using System.Text.Json;
using App;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Tests;

public class GameplayLifecycleTests
{
    private static WorldSnapshot Initial(Guid id, params ObjectState[] objects) => new(1, id, 0, false, objects);
    private static void Send(GameServer server, Guid id, WorldOrder order)
    {
        var endpoint = server.CreateGameplayEndpoint();
        Assert.Equal(DispatchResult.Accepted, endpoint.Accept(new GameMessage(id, "World", JsonSerializer.SerializeToElement(order))));
    }

    [Fact]
    public async Task EngineRotationFuelAndSnapshotAreExecutedInQueueOrder()
    {
        var server = new GameServer(new ServerOptions());
        var id = Guid.NewGuid(); var ship = Guid.NewGuid();
        try
        {
            server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0, Fuel: 3)));
            Send(server, id, new WorldOrder("EngineOn", ship));
            Send(server, id, new WorldOrder("Clockwise", ship));
            Send(server, id, new WorldOrder("Tick"));
            Send(server, id, new WorldOrder("Tick"));
            server.Start();
            var state = await server.SnapshotAsync(id);
            var body = Assert.Single(state.Objects);
            Assert.Equal(1, body.X); Assert.Equal(1, body.Y);
            Assert.Equal(1, body.Direction); Assert.Equal(1, body.Fuel);
            Assert.False(body.Engine); Assert.Equal(0, body.Rotation);
            Assert.Equal(2, state.Tick);
            Send(server, id, new WorldOrder("Refuel", ship, Amount: 5));
            var next = await server.SnapshotAsync(id);
            Assert.Equal(6, next.Objects[0].Fuel);
            Assert.Equal(1, state.Objects[0].Fuel);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task InsufficientFuelRejectsShotAndTransferIsConservative()
    {
        var errors = new List<GameDiagnostic>();
        var server = new GameServer(new ServerOptions(), errors.Add);
        var id = Guid.NewGuid(); var ship = Guid.NewGuid(); var tanker = Guid.NewGuid();
        try
        {
            server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0, Fuel: 0), new ObjectState(tanker, "Tanker", 3, 0, Fuel: 10)));
            Send(server, id, new WorldOrder("Shoot", ship));
            Send(server, id, new WorldOrder("TransferFuel", tanker, ship, 4));
            server.Start();
            var state = await server.SnapshotAsync(id);
            Assert.Equal(4, state.Objects.Single(o => o.Id == ship).Fuel);
            Assert.Equal(6, state.Objects.Single(o => o.Id == tanker).Fuel);
            Assert.Single(errors, e => e.Kind == "CommandFailed");
            Assert.Equal(2, state.Objects.Length);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task RocketsMoveAndExpireAndMeteorsRotate()
    {
        var server = new GameServer(new ServerOptions { Gameplay = new GameplayOptions { RocketLifetime = 2 } });
        var id = Guid.NewGuid(); var ship = Guid.NewGuid(); var meteor = Guid.NewGuid();
        try
        {
            server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0), new ObjectState(meteor, "Meteor", 100, 100, Fuel: 0, Engine: true, Rotation: 1)));
            Send(server, id, new WorldOrder("Shoot", ship));
            Send(server, id, new WorldOrder("Tick"));
            server.Start();
            var first = await server.SnapshotAsync(id);
            Assert.Equal(3, first.Objects.Single(o => o.Kind == "Rocket").X);
            Assert.Equal(1, first.Objects.Single(o => o.Id == meteor).Direction);
            Send(server, id, new WorldOrder("Tick"));
            Assert.DoesNotContain((await server.SnapshotAsync(id)).Objects, o => o.Kind == "Rocket");
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task BonusAndDestructiveCollisionsUpdateWorldSafely()
    {
        var server = new GameServer(new ServerOptions());
        var id = Guid.NewGuid(); var ship = Guid.NewGuid();
        try
        {
            server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0, Fuel: 10), new ObjectState(Guid.NewGuid(), "Bonus", 0, 0)));
            Send(server, id, new WorldOrder("Tick"));
            server.Start();
            Assert.Equal(30, Assert.Single((await server.SnapshotAsync(id)).Objects).Fuel);
            Send(server, id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 0, 0)));
            Send(server, id, new WorldOrder("Tick"));
            var state = await server.SnapshotAsync(id);
            Assert.Empty(state.Objects); Assert.True(state.Finished);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task MigrationDrainsAcceptedCommandsAndSwitchesWorkerWithoutChangingScope()
    {
        var server = new GameServer(new ServerOptions { Games = new GameOptions { MaxCommandsPerQuantum = 1 } });
        var id = Guid.NewGuid(); var ship = Guid.NewGuid();
        try
        {
            var game = server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0)));
            for (var i = 0; i < 20; i++) Send(server, id, new WorldOrder("Refuel", ship, Amount: 1));
            var target = 1 - game.WorkerId!.Value;
            await server.MigrateAsync(id, target).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(target, game.WorkerId);
            for (var i = 0; i < 20; i++) Send(server, id, new WorldOrder("Refuel", ship, Amount: 1));
            Assert.Equal(140, Assert.Single((await server.SnapshotAsync(id)).Objects).Fuel);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task SnapshotStoreRejectsInvalidVersionsAndPreservesLastGoodFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var store = new SnapshotStore(path);
        try
        {
            var snapshot = Initial(Guid.NewGuid(), new ObjectState(Guid.NewGuid(), "Ship", 0, 0));
            await store.SaveAsync(new[] { snapshot });
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new[] { snapshot with { Version = 99 } }));
            Assert.Equal(snapshot.GameId, Assert.Single(await store.LoadAsync()).GameId);
            await File.WriteAllTextAsync(path, "{\"Version\":99,\"Games\":[]}");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task SoftStopAndRestartRestoreStateAndHttpEndpoint()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var options = new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { SnapshotPath = path }
        };
        var id = Guid.NewGuid(); var ship = Guid.NewGuid();
        var manager = new ServerManager(options, new[] { Initial(id, new ObjectState(ship, "Ship", 0, 0)) });
        try
        {
            await manager.StartAsync();
            using var client = new HttpClient();
            using var content = new StringContent(JsonSerializer.Serialize(new { gameId = id, command = "World", arguments = new WorldOrder("Refuel", ship, Amount: 7) }), System.Text.Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(manager.Address + "/commands", content);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
            await manager.RestartAsync();
            Assert.Equal(107, Assert.Single((await manager.Server.SnapshotAsync(id)).Objects).Fuel);
            await manager.SoftStopAsync();
            Assert.Equal(ServerState.Stopped, manager.State);
            Assert.True(File.Exists(path));
        }
        finally
        {
            await manager.HardStopAsync();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task HardStopDiscardsQueuedCommandsButDoesNotInterruptRunningCommand()
    {
        var options = new ServerOptions { Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" }, Lifecycle = new LifecycleOptions { RestoreOnStart = false } };
        var manager = new ServerManager(options);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var executed = 0;
        try
        {
            await manager.StartAsync();
            var game = manager.Server.CreateGame(Guid.NewGuid());
            Assert.Equal(DispatchResult.Accepted, manager.Server.Dispatcher.Dispatch(game.Id, new ActionCommand(() => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); Interlocked.Increment(ref executed); })));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(DispatchResult.Accepted, manager.Server.Dispatcher.Dispatch(game.Id, new ActionCommand(() => Interlocked.Increment(ref executed))));
            var stopping = manager.HardStopAsync();
            release.Set();
            await stopping;
            Assert.Equal(1, executed);
            Assert.Equal(ServerState.Stopped, manager.State);
        }
        finally { release.Set(); await manager.HardStopAsync(); }
    }

    [Fact]
    public async Task EmptyServerCanStopAndStartAgain()
    {
        var options = new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { RestoreOnStart = false, SaveOnSoftStop = false }
        };
        var manager = new ServerManager(options);
        try
        {
            await Task.WhenAll(manager.StartAsync(), manager.StartAsync());
            await manager.SoftStopAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(ServerState.Stopped, manager.State);
            await manager.StartAsync();
            Assert.Equal(ServerState.Running, manager.State);
        }
        finally { await manager.HardStopAsync(); }
    }

    [Fact]
    public async Task HardStopTimeoutPreventsRestartUntilRunningCommandReturns()
    {
        var manager = new ServerManager(new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { RestoreOnStart = false, SaveOnSoftStop = false, TimeoutMilliseconds = 100 }
        });
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try
        {
            await manager.StartAsync();
            var game = manager.Server.CreateGame(Guid.NewGuid());
            Assert.Equal(DispatchResult.Accepted, manager.Server.Dispatcher.Dispatch(game.Id, new ActionCommand(() =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(10));
            })));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAsync<TimeoutException>(() => manager.HardStopAsync());
            Assert.Equal(ServerState.Stopping, manager.State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync());
        }
        finally
        {
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => manager.State == ServerState.Stopped, TimeSpan.FromSeconds(10)));
            await manager.HardStopAsync();
        }
    }

    [Fact]
    public async Task SnapshotWaitsForCapacityWithoutBypassingQueuedCommand()
    {
        var server = new GameServer(new ServerOptions { Games = new GameOptions { QueueCapacity = 1 } });
        var id = Guid.NewGuid();
        var ship = Guid.NewGuid();
        try
        {
            server.CreateWorld(Initial(id, new ObjectState(ship, "Ship", 0, 0)));
            Send(server, id, new WorldOrder("Refuel", ship, Amount: 7));
            var snapshot = server.SnapshotAsync(id);
            server.Start();
            Assert.Equal(107, Assert.Single((await snapshot).Objects).Fuel);
        }
        finally { server.Stop(); }
    }

    private class ActionCommand(Action action) : ICommand
    {
        public void Execute() => action();
    }
}
