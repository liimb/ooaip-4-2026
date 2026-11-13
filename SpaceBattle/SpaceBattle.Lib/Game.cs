using System.Diagnostics;
using App;

namespace SpaceBattle.Lib;

public class Game : ICommand
{
    private readonly object? _legacyScope;
    private readonly GameScope? _scope;
    private readonly Queue<ICommand> _commands = new();
    private readonly GameOptions _options;
    private readonly TimeProvider _clock;
    private readonly Action<GameDiagnostic> _diagnostics;
    private Worker? _worker;
    private Action? _release;
    private int _pending;
    private int _executing;
    private int _state;
    private int _cancelRequested;
    internal Action? AfterQuantum { get; set; }
    internal void CancelPending() => Interlocked.Exchange(ref _cancelRequested, 1);
    internal void ReserveControl() => Interlocked.Increment(ref _pending);
    internal void Rebind(Worker worker, Action release)
    {
        if (Volatile.Read(ref _executing) != 0 || PendingCount != 0)
            throw new InvalidOperationException("Game is not at a transfer boundary.");
        _worker = worker;
        _release = release;
    }

    public Game(Guid gameId, object scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Id = gameId;
        _legacyScope = scope;
        _options = new GameOptions();
        _clock = TimeProvider.System;
        _diagnostics = diagnostic => Trace.TraceError(diagnostic.ToString());
    }

    public Game(Guid gameId, GameScope scope, GameOptions? options = null,
        Action<GameDiagnostic>? diagnostics = null, TimeProvider? clock = null)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("Game ID must not be empty.", nameof(gameId));
        ArgumentNullException.ThrowIfNull(scope);
        Id = gameId;
        _scope = scope;
        _options = options ?? new GameOptions();
        _options.Validate();
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics ?? (diagnostic => Trace.TraceError(diagnostic.ToString()));
    }

    public Guid Id { get; }
    public GameState State => (GameState)Volatile.Read(ref _state);
    public int PendingCount => Volatile.Read(ref _pending);
    public int? WorkerId => _worker?.Id;
    internal bool Scheduled { get; set; }
    internal bool HasCommands => _commands.Count != 0;

    internal void Bind(Worker worker, Action release)
    {
        if (_scope == null || Interlocked.CompareExchange(ref _worker, worker, null) != null)
            throw new InvalidOperationException("Game cannot be assigned to this worker.");
        _release = release;
    }

    internal bool Reserve()
    {
        while (State != GameState.Completed)
        {
            var pending = PendingCount;
            if (pending >= _options.QueueCapacity) return false;
            if (Interlocked.CompareExchange(ref _pending, pending + 1, pending) == pending) return true;
        }
        return false;
    }

    internal void Release()
    {
        Interlocked.Decrement(ref _pending);
        _release?.Invoke();
    }

    internal void Enqueue(ICommand command)
    {
        RequireWorker();
        if (State == GameState.Completed)
        {
            Release();
            return;
        }
        _commands.Enqueue(command);
        SetState(GameState.Waiting);
    }

    public void Execute()
    {
        if (_legacyScope != null)
        {
            if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0)
                throw new InvalidOperationException("The game is already running.");
            try
            {
                SetState(GameState.Running);
                ExecuteLegacy();
            }
            finally
            {
                SetState(GameState.Inactive);
                Volatile.Write(ref _executing, 0);
            }
            return;
        }
        RequireWorker();
        if (Interlocked.CompareExchange(ref _executing, 1, 0) != 0)
            throw new InvalidOperationException("The game is already running.");
        GameScope.Activation? activation = null;
        try
        {
            activation = _scope!.Activate();
            SetState(GameState.Running);
            var started = _clock.GetTimestamp();
            var count = 0;
            while (_commands.TryDequeue(out var command))
            {
                var commandStarted = _clock.GetTimestamp();
                try
                {
                    if (Volatile.Read(ref _cancelRequested) == 0) command.Execute();
                }
                catch (Exception error) { Report(new GameDiagnostic(Id, "CommandFailed", TimeSpan.Zero, error)); }
                finally
                {
                    Release();
                    var duration = _clock.GetElapsedTime(commandStarted);
                    if (duration >= TimeSpan.FromMilliseconds(_options.SlowCommandMilliseconds))
                        Report(new GameDiagnostic(Id, "SlowCommand", duration));
                }
                count++;
                if (AfterQuantum != null || count >= _options.MaxCommandsPerQuantum ||
                    _clock.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(_options.QuantumMilliseconds)) break;
            }
        }
        finally
        {
            try { activation?.Restore(); }
            finally
            {
                SetState(_commands.Count == 0 ? GameState.Inactive : GameState.Waiting);
                Volatile.Write(ref _executing, 0);
            }
        }
    }

    internal void Abort(Exception error)
    {
        RequireWorker();
        SetState(GameState.Completed);
        while (_commands.TryDequeue(out _)) Release();
        Report(new GameDiagnostic(Id, "GameFailed", TimeSpan.Zero, error));
    }

    internal void Close()
    {
        if (PendingCount != 0 || Volatile.Read(ref _executing) != 0)
            throw new InvalidOperationException("Game still has accepted work.");
        SetState(GameState.Completed);
        _scope?.Close();
    }

    private void RequireWorker()
    {
        if (_worker?.IsCurrentThread != true)
            throw new InvalidOperationException("Game queue is accessible only on its assigned worker.");
    }
    internal void AssertContext()
    {
        RequireWorker();
        if (Volatile.Read(ref _executing) == 0) throw new InvalidOperationException("Game is not executing a command.");
    }

    private void SetState(GameState state) => Volatile.Write(ref _state, (int)state);

    private void Report(GameDiagnostic diagnostic)
    {
        try { _diagnostics(diagnostic); }
        catch (Exception error) { Trace.TraceError(error.ToString()); }
    }

    private void ExecuteLegacy()
    {
        var previous = Ioc.Resolve<object>("IoC.Scope.Current");
        var restore = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previous);
        try
        {
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _legacyScope!).Execute();
            while (Ioc.Resolve<bool>("Game.Scheduler.CanContinue"))
            {
                var command = Ioc.Resolve<ICommand>("Game.Scheduler.Take");
                try { command.Execute(); }
                catch (Exception error) { Ioc.Resolve<ICommand>("ExceptionHandler.Handle", command, error).Execute(); }
            }
        }
        finally { restore.Execute(); }
    }
}
