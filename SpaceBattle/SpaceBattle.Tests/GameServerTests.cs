using System.Collections.Concurrent;
using System.Text.Json;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameServerTests
{
    [Fact]
    public void QuantumUsesElapsedTimeAndRoundRobinWithoutInterruptingCommands()
    {
        var clock = new TestClock();
        var diagnostics = new List<GameDiagnostic>();
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1, IncomingBatchSize = 32 },
            Games = new GameOptions { QuantumMilliseconds = 10, SlowCommandMilliseconds = 5 }
        }, diagnostics.Add, clock);
        var order = new List<string>();
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                Accepted(server, first, () =>
                {
                    order.Add($"A{index}.start");
                    Assert.Equal(GameState.Running, first.State);
                    clock.Advance(TimeSpan.FromMilliseconds(11));
                    order.Add($"A{index}.end");
                });
            }
            Accepted(server, second, () => order.Add("B"));
            server.Stop();
            Assert.Equal(new[] { "A0.start", "A0.end", "B", "A1.start", "A1.end", "A2.start", "A2.end" }, order);
            Assert.Equal(3, diagnostics.Count(item => item.Kind == "SlowCommand"));
            Assert.DoesNotContain(diagnostics, item => item.Error != null);
            Assert.Equal(GameState.Completed, first.State);
            Assert.Equal(0, first.PendingCount);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void CommandLimitReschedulesFastGamesAndPreservesFifo()
    {
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1 },
            Games = new GameOptions { MaxCommandsPerQuantum = 1 }
        }, clock: new TestClock());
        var order = new List<int>();
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            Accepted(server, first, () => order.Add(1));
            Accepted(server, first, () => order.Add(3));
            Accepted(server, second, () => order.Add(2));
            Accepted(server, second, () => order.Add(4));
            server.Stop();
            Assert.Equal(new[] { 1, 2, 3, 4 }, order);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void ManyGamesShareFixedWorkersAndRestoreTheirOwnScopes()
    {
        var options = new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 2, QueueCapacity = 512 },
            MaxGames = 200
        };
        var errors = new ConcurrentBag<GameDiagnostic>();
        var server = new GameServer(options, errors.Add);
        var threads = new ConcurrentDictionary<int, byte>();
        var count = 0;
        try
        {
            for (var i = 0; i < 200; i++)
            {
                var value = i;
                var game = server.CreateGame(Guid.NewGuid(), scope => scope.Register("Test.Value", _ => value));
                Accepted(server, game, () =>
                {
                    Assert.Equal(value, Ioc.Resolve<int>("Test.Value"));
                    threads.TryAdd(Environment.CurrentManagedThreadId, 0);
                    Interlocked.Increment(ref count);
                });
            }
            server.Stop();
            Assert.Equal(200, count);
            Assert.Equal(2, threads.Count);
            Assert.Equal(2, server.WorkerCount);
            Assert.DoesNotContain(Environment.CurrentManagedThreadId, threads.Keys);
            Assert.DoesNotContain(errors, item => item.Error != null);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void StoppingUnstartedServerStartsAllWorkersAndExecutesGamesInParallel()
    {
        using var barrier = new Barrier(2);
        var errors = new ConcurrentBag<GameDiagnostic>();
        var server = new GameServer(new ServerOptions(), errors.Add);
        var count = 0;
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            Assert.NotEqual(first.WorkerId, second.WorkerId);
            foreach (var game in new[] { first, second })
                Accepted(server, game, () =>
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                    Interlocked.Increment(ref count);
                });
            server.Stop();
            Assert.Equal(2, count);
            Assert.DoesNotContain(errors, item => item.Error != null);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void ConcurrentProducersNeverExecuteOneGameConcurrentlyOrLoseAcceptedMessages()
    {
        var errors = new ConcurrentBag<GameDiagnostic>();
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 4, QueueCapacity = 64 },
            Games = new GameOptions { QueueCapacity = 64, MaxCommandsPerQuantum = 2 }
        }, errors.Add);
        var accepted = new ConcurrentBag<int>();
        var executed = new ConcurrentBag<int>();
        var active = 0;
        try
        {
            var game = server.CreateGame(Guid.NewGuid());
            server.Start();
            Parallel.For(0, 4000, id =>
            {
                var result = server.Dispatcher.Dispatch(game.Id, new TestAction(() =>
                {
                    Assert.Equal(1, Interlocked.Increment(ref active));
                    executed.Add(id);
                    Interlocked.Decrement(ref active);
                }));
                if (result == DispatchResult.Accepted) accepted.Add(id);
            });
            server.Stop();
            Assert.NotEmpty(accepted);
            Assert.Equal(accepted.Order(), executed.Order());
            Assert.Equal(executed.Count, executed.Distinct().Count());
            Assert.Equal(0, game.PendingCount);
            Assert.DoesNotContain(errors, item => item.Error != null);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void StoppingWhileProducersSendDrainsEveryAcceptedMessage()
    {
        var server = new GameServer(new ServerOptions());
        var accepted = 0;
        var executed = 0;
        try
        {
            var game = server.CreateGame(Guid.NewGuid());
            server.Start();
            Parallel.For(0, 2000, id =>
            {
                if (id == 1000) server.Stop();
                if (server.Dispatcher.Dispatch(game.Id, new TestAction(() => Interlocked.Increment(ref executed))) == DispatchResult.Accepted)
                    Interlocked.Increment(ref accepted);
            });
            server.Stop();
            Assert.Equal(accepted, executed);
            Assert.Equal(0, game.PendingCount);
            Assert.Equal(DispatchResult.Stopped, server.Dispatcher.Dispatch(game.Id, new TestAction(() => { })));
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void GameCapacityRejectsWithoutBlockingOtherGames()
    {
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1, QueueCapacity = 8 },
            Games = new GameOptions { QueueCapacity = 2 }
        });
        var count = 0;
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            Accepted(server, first, () => count++);
            Accepted(server, first, () => count++);
            Assert.Equal(DispatchResult.Overloaded, server.Dispatcher.Dispatch(first.Id, new TestAction(() => count++)));
            Accepted(server, second, () => count++);
            Assert.Equal(0, count);
            server.Stop();
            Assert.Equal(3, count);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void WorkerQueueRejectionReleasesGameCapacity()
    {
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1, QueueCapacity = 1 }
        });
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            Accepted(server, first, () => { });
            Assert.Equal(DispatchResult.Overloaded, server.Dispatcher.Dispatch(second.Id, new TestAction(() => { })));
            Assert.Equal(0, second.PendingCount);
            server.Stop();
            Assert.Equal(0, first.PendingCount);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void ErrorsInCommandsAndDiagnosticsDoNotStopOtherCommands()
    {
        var failures = 0;
        var count = 0;
        var server = new GameServer(new ServerOptions(), diagnostic =>
        {
            if (diagnostic.Kind == "CommandFailed") Interlocked.Increment(ref failures);
            throw new InvalidOperationException("diagnostic failure");
        });
        try
        {
            var first = server.CreateGame(Guid.NewGuid());
            var second = server.CreateGame(Guid.NewGuid());
            Accepted(server, first, () => throw new InvalidOperationException("command failure"));
            Accepted(server, first, () => Interlocked.Increment(ref count));
            Accepted(server, second, () => Interlocked.Increment(ref count));
            server.Stop();
            Assert.Equal(1, failures);
            Assert.Equal(2, count);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void IdleGameCanBeScheduledAgainAndCannotExecuteFromCaller()
    {
        var server = new GameServer(new ServerOptions());
        var count = 0;
        try
        {
            var game = server.CreateGame(Guid.NewGuid());
            Assert.Throws<InvalidOperationException>(game.Execute);
            Accepted(server, game, () => count++);
            server.Start();
            Assert.True(SpinWait.SpinUntil(() => game.State == GameState.Inactive, TimeSpan.FromSeconds(10)));
            Accepted(server, game, () => count++);
            server.Stop();
            Assert.Equal(2, count);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void EndpointValidatesAndCreatesCommandsOnlyInsideGameScope()
    {
        var server = new GameServer(new ServerOptions());
        var factoryThread = 0;
        var value = 0;
        try
        {
            var game = server.CreateGame(Guid.NewGuid(), scope => scope.Register("Test.Value", _ => 7));
            var endpoint = new Endpoint(server.Dispatcher, new Dictionary<string, Func<JsonElement, ICommand>>
            {
                ["Set"] = args =>
                {
                    factoryThread = Environment.CurrentManagedThreadId;
                    var resolved = Ioc.Resolve<int>("Test.Value");
                    return new TestAction(() => value = resolved + args.GetProperty("offset").GetInt32());
                }
            });
            using (var document = JsonDocument.Parse("{\"offset\":2}"))
            {
                Assert.Equal(DispatchResult.Accepted, endpoint.Accept(new GameMessage(game.Id, "Set", document.RootElement)));
                Assert.Equal(DispatchResult.UnknownGame, endpoint.Accept(new GameMessage(Guid.NewGuid(), "Set", document.RootElement)));
                Assert.Equal(DispatchResult.InvalidMessage, endpoint.Accept(new GameMessage(game.Id, "Unknown", document.RootElement)));
                Assert.Equal(DispatchResult.InvalidMessage, endpoint.Accept(new GameMessage(Guid.Empty, "Set", document.RootElement)));
            }
            Assert.Equal(0, factoryThread);
            Assert.Equal(DispatchResult.InvalidMessage, endpoint.Accept(new GameMessage(game.Id, "Set", default)));
            server.Stop();
            Assert.Equal(9, value);
            Assert.NotEqual(Environment.CurrentManagedThreadId, factoryThread);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void GameRejectsReentrantExecutionFromItsOwnCommand()
    {
        var errors = new List<GameDiagnostic>();
        var server = new GameServer(new ServerOptions(), errors.Add);
        var count = 0;
        try
        {
            var game = server.CreateGame(Guid.NewGuid());
            Accepted(server, game, () =>
            {
                Assert.Throws<InvalidOperationException>(game.Execute);
                Assert.Equal(GameState.Running, game.State);
                count++;
            });
            Accepted(server, game, () => count++);
            server.Stop();
            Assert.Equal(2, count);
            Assert.DoesNotContain(errors, item => item.Error != null);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void CreateGameValidatesIdentityAndLimitAndRestoresScopeAfterFailure()
    {
        var server = new GameServer(new ServerOptions { MaxGames = 1 });
        try
        {
            Assert.Throws<ArgumentException>(() => server.CreateGame(Guid.Empty));
            var id = Guid.NewGuid();
            Assert.Throws<InvalidOperationException>(() => server.CreateGame(id, _ => throw new InvalidOperationException("setup")));
            server.CreateGame(id);
            Assert.Throws<InvalidOperationException>(() => server.CreateGame(id));
            Assert.Throws<InvalidOperationException>(() => server.CreateGame(Guid.NewGuid()));
            server.Stop();
            Assert.Throws<InvalidOperationException>(server.Start);
        }
        finally { server.Stop(); }
    }

    [Theory]
    [InlineData("{\"MaxGames\":0}")]
    [InlineData("{\"Games\":{\"QueueCapacity\":0}}")]
    [InlineData("{\"Games\":{\"QuantumMilliseconds\":0}}")]
    [InlineData("{\"Games\":{\"MaxCommandsPerQuantum\":0}}")]
    [InlineData("{\"Games\":{\"SlowCommandMilliseconds\":0}}")]
    [InlineData("{\"Workers\":{\"IncomingBatchSize\":0}}")]
    [InlineData("{\"Endpoint\":{\"Url\":\"ftp://localhost\"}}")]
    [InlineData("{\"Endpoint\":{\"MaxRequestBytes\":0}}")]
    [InlineData("{\"Workers\":null}")]
    public void InvalidConfigurationIsRejected(string json)
    {
        Assert.ThrowsAny<ArgumentException>(() => ServerOptions.FromJson(json));
    }

    private static void Accepted(GameServer server, Game game, Action action) =>
        Assert.Equal(DispatchResult.Accepted, server.Dispatcher.Dispatch(game.Id, new TestAction(action)));

    private class TestAction(Action action) : ICommand
    {
        public void Execute() => action();
    }

    private class TestClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
    }
}
