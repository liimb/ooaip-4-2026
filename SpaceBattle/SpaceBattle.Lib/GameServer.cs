using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
using App;

namespace SpaceBattle.Lib;

public class GameServer
{
    private readonly object _lifecycle = new();
    private readonly ServerOptions _options;
    private readonly WorkerPool _pool;
    private readonly GameScheduler[] _schedulers;
    private readonly ConcurrentDictionary<Guid, Game> _games = new();
    private readonly ConcurrentDictionary<Guid, GameWorld> _worlds = new();
    private readonly ConcurrentDictionary<Guid, MatchSession> _sessions = new();
    private readonly Action<GameDiagnostic> _diagnostics;
    private readonly TimeProvider _clock;
    private bool _stopped;

    public GameServer(ServerOptions options, Action<GameDiagnostic>? diagnostics = null, TimeProvider? clock = null, IRatingPolicy? ratingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _diagnostics = diagnostics ?? (diagnostic => Trace.TraceError(diagnostic.ToString()));
        _clock = clock ?? TimeProvider.System;
        _pool = new WorkerPool(options.Workers);
        _schedulers = _pool.Workers.Select(worker => new GameScheduler(worker)).ToArray();
        Results = new MatchResults(options.Results, ratingPolicy ?? new EloRatingPolicy(options.Results.RatingK,
            options.Matchmaking.MinRating, options.Matchmaking.MaxRating));
        Matchmaking = new Matchmaker(this, options.Matchmaking, _clock);
    }

    public Dispatcher Dispatcher { get; } = new();
    public int WorkerCount => _pool.Workers.Count;
    public Matchmaker Matchmaking { get; }
    public MatchResults Results { get; }

    public Game CreateGame(Guid gameId, Action<GameScope>? configure = null)
    {
        lock (_lifecycle)
        {
            if (_stopped) throw new InvalidOperationException("Server is stopped.");
            if (gameId == Guid.Empty) throw new ArgumentException("Game ID must not be empty.", nameof(gameId));
            if (_games.ContainsKey(gameId)) throw new InvalidOperationException("Game ID is already registered.");
            if (_games.Count >= _options.MaxGames) throw new InvalidOperationException("Game limit reached.");
            var scope = new GameScope();
            try
            {
                var activation = scope.Activate();
                try { configure?.Invoke(scope); }
                finally { activation.Restore(); }
                var game = new Game(gameId, scope, _options.Games, _diagnostics, _clock);
                var scheduler = _schedulers.OrderBy(item => item.PendingCount).ThenBy(item => item.GameCount).First();
                scheduler.Assign(game);
                _games.TryAdd(gameId, game);
                Dispatcher.Register(game, scheduler);
                return game;
            }
            catch
            {
                scope.Close();
                throw;
            }
        }
    }

    public void Start()
    {
        lock (_lifecycle)
        {
            if (_stopped) throw new InvalidOperationException("Server is stopped.");
            _pool.Start();
        }
        Matchmaking.Start();
    }

    public void Stop()
    {
        if (_pool.Workers.Any(worker => worker.IsCurrentThread))
            throw new InvalidOperationException("Stop the server from its controlling thread.");
        Matchmaking.Stop();
        Game[] games;
        lock (_lifecycle)
        {
            _stopped = true;
            Dispatcher.StopAccepting();
            games = _games.Values.ToArray();
        }
        _pool.Stop();
        var errors = new List<Exception>();
        foreach (var game in games)
        {
            try { game.Close(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0) throw new AggregateException(errors);
    }

    public Game CreateWorld(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Match != null ? Matchmaking.Restore(snapshot) : CreateWorldCore(snapshot, null);
    }

    internal MatchSession CreateMatch(MatchDefinition definition)
    {
        var rules = _options.Matchmaking;
        var snapshot = new WorldSnapshot(1, definition.Id, 0, false, new[]
        {
            new ObjectState(definition.First.ShipId, "Ship", 0, 0, Fuel: rules.InitialShipFuel, Rockets: rules.InitialShipRockets, Owner: definition.First.Id),
            new ObjectState(definition.Second.ShipId, "Ship", rules.SpawnDistance, 0, Direction: 4, Fuel: rules.InitialShipFuel, Rockets: rules.InitialShipRockets, Owner: definition.Second.Id)
        }, definition);
        return RestoreMatch(snapshot);
    }

    internal MatchSession RestoreMatch(WorldSnapshot snapshot)
    {
        MatchSession? session = null;
        var game = CreateWorldCore(snapshot, (scope, world) =>
        {
            session = new MatchSession(snapshot, world, snapshot.Progress?.Rules ?? _options.Gameplay, Results, _options.Results, _clock);
            scope.Register("Match.Definition", _ => snapshot.Match!);
        });
        session!.Game = game;
        _sessions[snapshot.GameId] = session;
        return session;
    }

    private Game CreateWorldCore(WorldSnapshot snapshot, Action<GameScope, GameWorld>? configure)
    {
        GameWorld.Validate(snapshot);
        if (snapshot.Objects.Length > _options.Gameplay.MaxObjects || snapshot.Objects.Any(item => item.Fuel > _options.Gameplay.MaxFuel))
            throw new ArgumentException("Snapshot exceeds configured limits.");
        lock (_lifecycle)
        {
            GameWorld? world = null;
            var game = CreateGame(snapshot.GameId, scope =>
            {
                world = new GameWorld(snapshot, snapshot.Progress?.Rules ?? _options.Gameplay, () => _games[snapshot.GameId].AssertContext());
                scope.Register("World.Command", args => world.Command((WorldOrder)args[0]));
                scope.Register("Game.Operations.Shooting", args => world.Shooting((IShootable)args[0]));
                scope.Register("Game.Registry.Get", args => world.MovingObject((Guid)args[0]));
                configure?.Invoke(scope, world);
            });
            _worlds[snapshot.GameId] = world!;
            return game;
        }
    }

    public Endpoint CreateGameplayEndpoint() => new(Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
    {
        ["World"] = args => Ioc.Resolve<ICommand>("World.Command", args.Deserialize<WorldOrder>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Order is required."))
    });

    public async Task<WorldSnapshot> SnapshotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (Results.Committed(id) is { } result)
            return result.Final with { Objects = result.Final.Objects.ToArray() };
        return await InvokeAsync(id, () => _sessions.TryGetValue(id, out var session) ? session.Snapshot() : _worlds[id].Snapshot(), cancellationToken);
    }

    public async Task<MatchResult?> FinishMatchAsync(Guid id, MatchEndReason reason = MatchEndReason.Interrupted,
        Guid? playerId = null, CancellationToken token = default)
    {
        if (Results.Committed(id) is { } committed) return await Results.GetAsync(committed.MatchId, token);
        await InvokeAsync(id, () =>
        {
            if (!_sessions.TryGetValue(id, out var session)) throw new ArgumentException("Game is not a match.");
            if (session.Outcome == null) _worlds[id].Command(new WorldOrder("Finish", Reason: reason, PlayerId: playerId)).Execute();
            return true;
        }, token);
        await Results.FlushAsync(token);
        await RetireCompletedMatchesAsync(token);
        return await Results.GetAsync(id, token);
    }

    internal async Task RetireCompletedMatchesAsync(CancellationToken token = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_options.Lifecycle.TimeoutMilliseconds);
        foreach (var session in _sessions.Values.Where(session => session.IsCommitted))
        {
            while (!await Dispatcher.Retire(session.Definition.Id).WaitAsync(deadline.Token))
                await Task.Delay(1, deadline.Token);
            lock (_lifecycle)
            {
                _sessions.TryRemove(session.Definition.Id, out _);
                _worlds.TryRemove(session.Definition.Id, out _);
                _games.TryRemove(session.Definition.Id, out _);
            }
        }
    }

    internal void ReportResultFailure(Exception error)
    {
        try { _diagnostics(new GameDiagnostic(Guid.Empty, "ResultSaveFailed", TimeSpan.Zero, error)); }
        catch (Exception reportingError) { Trace.TraceError(reportingError.ToString()); }
    }

    public async Task FinishMatchesAsync(MatchEndReason reason, CancellationToken token = default)
    {
        foreach (var id in _sessions.Keys) await FinishMatchAsync(id, reason, token: token);
    }

    public async Task<WorldSnapshot[]> SnapshotAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<WorldSnapshot>();
        foreach (var id in _worlds.Keys)
            if (!Results.IsCommitted(id)) snapshots.Add(await SnapshotAsync(id, cancellationToken));
        return snapshots.ToArray();
    }

    public Task MigrateAsync(Guid id, int workerId)
    {
        if (workerId < 0 || workerId >= _schedulers.Length) throw new ArgumentOutOfRangeException(nameof(workerId));
        Start();
        return Dispatcher.Transfer(id, _schedulers[workerId]);
    }

    public void Freeze()
    {
        Matchmaking.Stop();
        lock (_lifecycle)
        {
            _stopped = true;
            Dispatcher.StopAccepting();
            _pool.Start();
        }
    }

    public void CancelPending()
    {
        Freeze();
        Dispatcher.CancelTransfers();
        foreach (var game in _games.Values) game.CancelPending();
    }

    private async Task<T> InvokeAsync<T>(Guid id, Func<T> action, CancellationToken token)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ServiceCommand(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (Exception error) { completion.TrySetException(error); }
        });
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_options.Lifecycle.TimeoutMilliseconds);
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var result = Dispatcher.Service(id, command);
            if (result == DispatchResult.Accepted) break;
            if (result != DispatchResult.Overloaded) throw new InvalidOperationException(result.ToString());
            await Task.Delay(1, deadline.Token);
        }
        return await completion.Task.WaitAsync(deadline.Token);
    }

    internal class ServiceCommand(Action action) : ICommand
    {
        public void Execute() => action();
    }
}
