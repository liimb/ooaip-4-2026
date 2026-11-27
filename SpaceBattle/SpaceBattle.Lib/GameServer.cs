using System.Diagnostics;

namespace SpaceBattle.Lib;

public class GameServer
{
    private readonly object _lifecycle = new();
    private readonly ServerOptions _options;
    private readonly WorkerPool _pool;
    private readonly GameScheduler[] _schedulers;
    private readonly Dictionary<Guid, Game> _games = new();
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
                _games.Add(gameId, game);
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
}
