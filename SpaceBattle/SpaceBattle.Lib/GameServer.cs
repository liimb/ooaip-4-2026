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
    private readonly Action<GameDiagnostic> _diagnostics;
    private readonly TimeProvider _clock;
    private bool _stopped;

    public GameServer(ServerOptions options, Action<GameDiagnostic>? diagnostics = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _diagnostics = diagnostics ?? (diagnostic => Trace.TraceError(diagnostic.ToString()));
        _clock = clock ?? TimeProvider.System;
        _pool = new WorkerPool(options.Workers);
        _schedulers = _pool.Workers.Select(worker => new GameScheduler(worker)).ToArray();
    }

    public Dispatcher Dispatcher { get; } = new();
    public int WorkerCount => _pool.Workers.Count;

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
    }

    public void Stop()
    {
        if (_pool.Workers.Any(worker => worker.IsCurrentThread))
            throw new InvalidOperationException("Stop the server from its controlling thread.");
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
        GameWorld.Validate(snapshot);
        if (snapshot.Objects.Length > _options.Gameplay.MaxObjects || snapshot.Objects.Any(item => item.Fuel > _options.Gameplay.MaxFuel))
            throw new ArgumentException("Snapshot exceeds configured limits.");
        lock (_lifecycle)
        {
            var game = CreateGame(snapshot.GameId, scope =>
            {
                var world = new GameWorld(snapshot, _options.Gameplay, () => _games[snapshot.GameId].AssertContext());
                scope.Register("World.Command", args => world.Command((WorldOrder)args[0]));
                scope.Register("Game.Operations.Shooting", args => world.Shooting((IShootable)args[0]));
                scope.Register("Game.Registry.Get", args => world.MovingObject((Guid)args[0]));
                _worlds[snapshot.GameId] = world;
            });
            return game;
        }
    }

    public Endpoint CreateGameplayEndpoint() => new(Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
    {
        ["World"] = args => Ioc.Resolve<ICommand>("World.Command", args.Deserialize<WorldOrder>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("Order is required."))
    });

    public Task<WorldSnapshot> SnapshotAsync(Guid id, CancellationToken cancellationToken = default) =>
        InvokeAsync(id, () => _worlds[id].Snapshot(), cancellationToken);

    public async Task<WorldSnapshot[]> SnapshotAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<WorldSnapshot>();
        foreach (var id in _worlds.Keys) snapshots.Add(await SnapshotAsync(id, cancellationToken));
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

    private class ServiceCommand(Action action) : ICommand
    {
        public void Execute() => action();
    }
}
