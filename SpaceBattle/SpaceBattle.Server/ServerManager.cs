using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public enum ServerState { Stopped, Starting, Running, Stopping, Faulted }

public class ServerManager
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly ServerOptions _options;
    private readonly SnapshotStore _store;
    private readonly WorldSnapshot[] _initial;
    private WebApplication? _host;
    private GameServer? _server;
    private Task? _shutdown;
    private volatile ServerState _state;
    public ServerState State => _state;
    public GameServer Server => _server ?? throw new InvalidOperationException("Server has not started.");
    public string? Address => _host?.Urls.FirstOrDefault();

    public ServerManager(ServerOptions options, IEnumerable<WorldSnapshot>? initial = null)
    {
        options.Validate();
        _options = options;
        _store = new SnapshotStore(options.Lifecycle.SnapshotPath);
        _initial = initial?.ToArray() ?? Array.Empty<WorldSnapshot>();
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        await _lifecycle.WaitAsync(token);
        try { await StartCore(token); }
        finally { _lifecycle.Release(); }
    }

    private async Task StartCore(CancellationToken token)
    {
        if (_state == ServerState.Running) return;
        if (_shutdown is { IsCompleted: false }) throw new InvalidOperationException("Previous server is still stopping.");
        _state = ServerState.Starting;
        var server = new GameServer(_options, diagnostic => Console.Error.WriteLine(diagnostic));
        WebApplication? host = null;
        try
        {
            var snapshots = _options.Lifecycle.RestoreOnStart && File.Exists(_options.Lifecycle.SnapshotPath)
                ? await _store.LoadAsync(token) : _initial;
            foreach (var snapshot in snapshots) server.CreateWorld(snapshot);
            host = ServerApplication.Create(_options, server.CreateGameplayEndpoint());
            server.Start();
            await host.StartAsync(token);
            _host = host;
            _server = server;
            _shutdown = null;
            _state = ServerState.Running;
        }
        catch
        {
            try
            {
                try { if (host != null) await host.DisposeAsync(); }
                finally { server.Stop(); }
            }
            finally { _state = ServerState.Faulted; }
            throw;
        }
    }

    public Task SoftStopAsync(CancellationToken token = default) => StopAsync(false, token);
    public Task HardStopAsync(CancellationToken token = default) => StopAsync(true, token);

    private async Task StopAsync(bool hard, CancellationToken token)
    {
        await _lifecycle.WaitAsync(token);
        try
        {
            if (_server == null || _state == ServerState.Stopped) return;
            if (hard) _server.CancelPending();
            _shutdown ??= ShutdownCore(hard);
            await _shutdown.WaitAsync(TimeSpan.FromMilliseconds(_options.Lifecycle.TimeoutMilliseconds), token);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task ShutdownCore(bool hard)
    {
        _state = ServerState.Stopping;
        var server = Server;
        server.Freeze();
        try
        {
            if (!hard && _options.Lifecycle.SaveOnSoftStop)
                await _store.SaveAsync(await server.SnapshotAllAsync());
        }
        finally
        {
            try
            {
                try { await Task.Run(server.Stop); }
                finally
                {
                    if (_host != null)
                    {
                        try { await _host.StopAsync(); }
                        finally { await _host.DisposeAsync(); }
                    }
                }
            }
            finally { _state = ServerState.Stopped; }
        }
    }

    public async Task RestartAsync(CancellationToken token = default)
    {
        await _lifecycle.WaitAsync(token);
        try
        {
            if (_server != null && _state != ServerState.Stopped)
            {
                _shutdown ??= ShutdownCore(false);
                await _shutdown.WaitAsync(TimeSpan.FromMilliseconds(_options.Lifecycle.TimeoutMilliseconds), token);
            }
            await StartCore(token);
        }
        finally { _lifecycle.Release(); }
    }
}
