using System.Text.Json;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MatchmakingTests
{
    private static ServerOptions Options(MatchmakingOptions? matchmaking = null, int maxGames = 1024) => new()
    {
        MaxGames = maxGames,
        Matchmaking = matchmaking ?? new MatchmakingOptions { PollIntervalMilliseconds = 60000 }
    };

    [Fact]
    public async Task JoinRejectsDuplicatesAndCancelReleasesTicketAndPlayer()
    {
        var server = new GameServer(Options());
        var player = Guid.NewGuid();
        try
        {
            Assert.Equal(SearchState.NotSearching, server.Matchmaking.Status(player).State);
            var ticket = server.Matchmaking.Join(player, 1200);
            Assert.Equal(SearchState.Searching, server.Matchmaking.Status(player).State);
            Assert.False(ticket.WaitAsync().IsCompleted);
            Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(player, 1200));
            Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(player, 1200, MatchKind.Training));
            Assert.True(server.Matchmaking.Cancel(player, ticket.Id));
            Assert.False(server.Matchmaking.Cancel(player, ticket.Id));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ticket.WaitAsync());
            Assert.Equal(SearchState.NotSearching, server.Matchmaking.Status(player).State);
            var next = server.Matchmaking.Join(player, 1200);
            Assert.NotEqual(ticket.Id, next.Id);
            Assert.False(server.Matchmaking.Cancel(player, ticket.Id));
            Assert.Equal(next.Id, server.Matchmaking.Status(player).SearchId);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task OldestPlayerGetsClosestEligibleOpponentAndBothReceiveSameMatch()
    {
        var server = new GameServer(Options());
        try
        {
            var first = server.Matchmaking.Join(Guid.NewGuid(), 1200);
            var far = server.Matchmaking.Join(Guid.NewGuid(), 1290);
            var close = server.Matchmaking.Join(Guid.NewGuid(), 1201);
            server.Matchmaking.Process();
            var match = await first.WaitAsync();
            Assert.Equal(match, await close.WaitAsync());
            Assert.False(far.WaitAsync().IsCompleted);
            Assert.Equal(close.PlayerId, match.Match.Second.Id);
            Assert.True(match.Match.IsRanked);
            Assert.Equal(MatchKind.Duel, match.Match.Kind);
            Assert.InRange(match.WorkerId, 0, server.WorkerCount - 1);
            Assert.Equal(SearchState.InGame, server.Matchmaking.Status(first.PlayerId).State);
            Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(first.PlayerId, 1200));
            Assert.False(server.Matchmaking.Cancel(first.PlayerId, first.Id));
            Assert.Equal(match, await server.Matchmaking.WaitAsync(first.PlayerId, first.Id));
            server.Start();
            var snapshot = await server.SnapshotAsync(match.Match.Id);
            Assert.Equal(match.Match, snapshot.Match);
            Assert.Equal(2, snapshot.Objects.Length);
            Assert.Contains(snapshot.Objects, item => item.Id == match.Match.First.ShipId && item.Owner == first.PlayerId);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task RangeExpandsForEachPlayerIndependentlyAndStopsAtMaximum()
    {
        var clock = new ManualClock();
        var config = new MatchmakingOptions { InitialRatingRange = 10, RatingRangeStep = 10, MaxRatingRange = 30, ExpansionIntervalMilliseconds = 100, AllowDuelBots = false };
        var server = new GameServer(Options(config), clock: clock);
        try
        {
            var old = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            clock.Advance(200);
            var fresh = server.Matchmaking.Join(Guid.NewGuid(), 1030);
            server.Matchmaking.Process();
            Assert.False(old.WaitAsync().IsCompleted);
            clock.Advance(199);
            server.Matchmaking.Process();
            Assert.False(fresh.WaitAsync().IsCompleted);
            clock.Advance(1);
            server.Matchmaking.Process();
            Assert.Equal(await old.WaitAsync(), await fresh.WaitAsync());
            var alone = server.Matchmaking.Join(Guid.NewGuid(), 2000);
            server.Matchmaking.Join(Guid.NewGuid(), 2031);
            clock.Advance(1000000);
            server.Matchmaking.Process();
            Assert.False(alone.WaitAsync().IsCompleted);
            Assert.Equal(30, config.Range(TimeSpan.MaxValue));
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task RejoiningAtSameTimestampGoesBehindPlayersAlreadyWaiting()
    {
        var clock = new ManualClock();
        var server = new GameServer(Options(), clock: clock);
        try
        {
            var cancelled = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var oldest = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var next = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            server.Matchmaking.Cancel(cancelled.PlayerId, cancelled.Id);
            var rejoined = server.Matchmaking.Join(cancelled.PlayerId, 1000);
            server.Matchmaking.Process();
            var found = await oldest.WaitAsync();
            Assert.Equal(oldest.PlayerId, found.Match.First.Id);
            Assert.Equal(next.PlayerId, found.Match.Second.Id);
            Assert.False(rejoined.WaitAsync().IsCompleted);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task InvalidRestoredOwnershipDoesNotReservePlayersOrCreateGame()
    {
        var server = new GameServer(Options());
        var restored = new GameServer(Options(maxGames: 1));
        try
        {
            var ticket = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            server.Matchmaking.Process();
            var found = await ticket.WaitAsync();
            server.Start();
            var state = await server.SnapshotAsync(found.Match.Id);
            var invalid = state with { Objects = state.Objects.Select(item => item.Id == found.Match.First.ShipId ? item with { Owner = Guid.NewGuid() } : item).ToArray() };
            Assert.Throws<ArgumentException>(() => restored.CreateWorld(invalid));
            Assert.Equal(SearchState.NotSearching, restored.Matchmaking.Status(ticket.PlayerId).State);
            restored.CreateWorld(state);
            Assert.Equal(SearchState.InGame, restored.Matchmaking.Status(ticket.PlayerId).State);
        }
        finally { server.Stop(); restored.Stop(); }
    }

    [Fact]
    public async Task BotAppearsOnlyAtTimeoutAndUsesNormalGameCommands()
    {
        var clock = new ManualClock();
        var server = new GameServer(Options(new MatchmakingOptions { BotWaitMilliseconds = 1000, PollIntervalMilliseconds = 60000 }), clock: clock);
        try
        {
            var ticket = server.Matchmaking.Join(Guid.NewGuid(), 1500);
            clock.Advance(999);
            server.Matchmaking.Process();
            Assert.False(ticket.WaitAsync().IsCompleted);
            clock.Advance(1);
            server.Matchmaking.Process();
            var match = await ticket.WaitAsync();
            Assert.True(match.Match.Second.IsBot);
            Assert.False(match.Match.IsRanked);
            Assert.Equal(1500, match.Match.Second.Rating);
            server.Start();
            var snapshot = await server.SnapshotAsync(match.Match.Id);
            Assert.Equal(1, snapshot.Tick);
            Assert.Contains(snapshot.Objects, item => item.Kind == "Rocket" && item.Owner == match.Match.Second.ShipId);
            Assert.Equal(4, snapshot.Objects.Single(item => item.Id == match.Match.Second.ShipId).Rockets);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task HumanOpponentTakesPriorityOverBotAtDeadline()
    {
        var clock = new ManualClock();
        var server = new GameServer(Options(), clock: clock);
        try
        {
            var first = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            clock.Advance(120000);
            var second = server.Matchmaking.Join(Guid.NewGuid(), 1001);
            server.Matchmaking.Process();
            var found = await first.WaitAsync();
            Assert.Equal(found, await second.WaitAsync());
            Assert.False(found.Match.Second.IsBot);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task TrainingDoesNotConsumeDuelOpponentOrAffectRating()
    {
        var server = new GameServer(Options());
        try
        {
            var duel = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var training = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            server.Matchmaking.Process();
            var found = await training.WaitAsync();
            Assert.Equal(MatchKind.Training, found.Match.Kind);
            Assert.False(found.Match.IsRanked);
            Assert.True(found.Match.Second.IsBot);
            Assert.Equal(1000, found.Match.First.Rating);
            Assert.False(duel.WaitAsync().IsCompleted);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void CreationFailurePreservesQueueOrderWaitingTimeAndCancellation()
    {
        var clock = new ManualClock();
        var server = new GameServer(Options(maxGames: 1), clock: clock);
        try
        {
            server.CreateGame(Guid.NewGuid());
            var first = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var second = server.Matchmaking.Join(Guid.NewGuid(), 1100);
            server.Matchmaking.Process();
            Assert.False(first.WaitAsync().IsCompleted);
            Assert.False(second.WaitAsync().IsCompleted);
            Assert.Equal(SearchState.Searching, server.Matchmaking.Status(first.PlayerId).State);
            Assert.Contains("limit", server.Matchmaking.Status(first.PlayerId).Error);
            clock.Advance(120000);
            server.Matchmaking.Process();
            Assert.Equal(first.Id, server.Matchmaking.Status(first.PlayerId).SearchId);
            Assert.Equal(second.Id, server.Matchmaking.Status(second.PlayerId).SearchId);
            Assert.True(server.Matchmaking.Cancel(first.PlayerId, first.Id));
            Assert.True(server.Matchmaking.Cancel(second.PlayerId, second.Id));
            Assert.NotEqual(first.Id, server.Matchmaking.Join(first.PlayerId, 1000).Id);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task ConcurrentJoiningNeverLosesOrMatchesAPlayerTwice()
    {
        var server = new GameServer(Options());
        try
        {
            var ids = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
            var tickets = await Task.WhenAll(ids.Select(id => Task.Run(() => server.Matchmaking.Join(id, 1000))));
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(server.Matchmaking.Process)));
            var found = await Task.WhenAll(tickets.Select(ticket => ticket.WaitAsync()));
            Assert.Equal(20, found.Select(item => item.Match.Id).Distinct().Count());
            var players = found.DistinctBy(item => item.Match.Id).SelectMany(item => new[] { item.Match.First.Id, item.Match.Second.Id }).ToArray();
            Assert.Equal(ids.Order(), players.Order());
            Assert.All(ids, id => Assert.Equal(SearchState.InGame, server.Matchmaking.Status(id).State));
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task ConcurrentDuplicateJoinsAdmitExactlyOneSearch()
    {
        var server = new GameServer(Options());
        try
        {
            var id = Guid.NewGuid();
            var successes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                try { server.Matchmaking.Join(id, 1000); return true; }
                catch (InvalidOperationException) { return false; }
            })));
            Assert.Single(successes, value => value);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task ConcurrentCancellationAndMatchingHaveOneUnambiguousWinner()
    {
        var server = new GameServer(Options());
        try
        {
            for (var i = 0; i < 30; i++)
            {
                var first = server.Matchmaking.Join(Guid.NewGuid(), 1000);
                var second = server.Matchmaking.Join(Guid.NewGuid(), 1000);
                var cancel = Task.Run(() => server.Matchmaking.Cancel(first.PlayerId, first.Id));
                await Task.WhenAll(cancel, Task.Run(server.Matchmaking.Process));
                if (await cancel)
                {
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync());
                    Assert.Equal(SearchState.NotSearching, server.Matchmaking.Status(first.PlayerId).State);
                    Assert.True(server.Matchmaking.Cancel(second.PlayerId, second.Id));
                }
                else
                {
                    Assert.Equal(await first.WaitAsync(), await second.WaitAsync());
                    Assert.Equal(SearchState.InGame, server.Matchmaking.Status(first.PlayerId).State);
                }
            }
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task ConcurrentBotTimeoutAndOpponentArrivalCannotCreateTwoMatches()
    {
        var clock = new ManualClock();
        var server = new GameServer(Options(), clock: clock);
        try
        {
            var first = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            clock.Advance(120000);
            var arrival = Task.Run(() => server.Matchmaking.Join(Guid.NewGuid(), 1000));
            await Task.WhenAll(arrival, Task.Run(server.Matchmaking.Process), Task.Run(server.Matchmaking.Process));
            var match = await first.WaitAsync();
            var second = await arrival;
            if (match.Match.Second.IsBot) Assert.Equal(SearchState.Searching, server.Matchmaking.Status(second.PlayerId).State);
            else Assert.Equal(match, await second.WaitAsync());
            Assert.Equal(match, server.Matchmaking.Status(first.PlayerId).Match);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task CancellingNotificationWaitDoesNotCancelSearch()
    {
        var server = new GameServer(Options());
        using var stop = new CancellationTokenSource();
        try
        {
            var ticket = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var waiting = ticket.WaitAsync(stop.Token);
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Equal(SearchState.Searching, server.Matchmaking.Status(ticket.PlayerId).State);
            server.Matchmaking.Join(Guid.NewGuid(), 1000);
            server.Matchmaking.Process();
            Assert.NotNull(await ticket.WaitAsync());
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task FinishRecordsOneOutcomeAndAllowsPlayersToSearchAgain()
    {
        var server = new GameServer(Options());
        try
        {
            var first = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            var second = server.Matchmaking.Join(Guid.NewGuid(), 1000);
            server.Matchmaking.Process();
            var found = await first.WaitAsync();
            var id = found.Match.Id;
            server.Start();
            await server.SnapshotAsync(id);
            Send(server, id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 30, 0)));
            server.Matchmaking.Process();
            var final = await server.SnapshotAsync(id);
            Assert.True(final.Finished);
            var outcome = server.Matchmaking.Outcome(id);
            Assert.Equal(first.PlayerId, outcome!.WinnerId);
            server.Matchmaking.Process();
            Assert.Same(outcome, server.Matchmaking.Outcome(id));
            await server.Results.FlushAsync();
            Assert.Equal(SearchState.NotSearching, server.Matchmaking.Status(second.PlayerId).State);
            Assert.NotEqual(first.Id, server.Matchmaking.Join(first.PlayerId, 1000).Id);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task MatchScopesAndWorldsAreIsolatedAndMetadataSurvivesRestore()
    {
        var server = new GameServer(Options());
        var restored = new GameServer(Options());
        try
        {
            var a = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            var b = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            server.Matchmaking.Process();
            var first = await a.WaitAsync();
            var second = await b.WaitAsync();
            var completion = new TaskCompletionSource<MatchDefinition>(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.Equal(DispatchResult.Accepted, server.Dispatcher.Dispatch(first.Match.Id,
                new ReadMatchCommand(completion)));
            server.Start();
            Assert.Equal(first.Match, await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            var snapshot = await server.SnapshotAsync(first.Match.Id);
            var other = await server.SnapshotAsync(second.Match.Id);
            Assert.DoesNotContain(other.Objects, item => item.Id == first.Match.First.ShipId);
            var saved = JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(snapshot))!;
            restored.CreateWorld(saved);
            Assert.Throws<InvalidOperationException>(() => restored.Matchmaking.Join(a.PlayerId, 1000));
            restored.Start();
            Assert.Equal(first.Match, (await restored.SnapshotAsync(first.Match.Id)).Match);
        }
        finally { server.Stop(); restored.Stop(); }
    }

    [Fact]
    public async Task StoppingCancelsWaitingTicketsAndRejectsNewSearches()
    {
        var server = new GameServer(Options());
        var ticket = server.Matchmaking.Join(Guid.NewGuid(), 1000);
        server.Start();
        await server.Matchmaking.StopAsync();
        server.Stop();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ticket.WaitAsync());
        Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(Guid.NewGuid(), 1000));
        Assert.Equal(SearchState.NotSearching, server.Matchmaking.Status(ticket.PlayerId).State);
    }

    [Fact]
    public async Task OverloadedWorkerRetriesMatchStepWithoutLosingOrDuplicatingIt()
    {
        var server = new GameServer(new ServerOptions
        {
            Workers = new WorkerOptions { WorkerCount = 1, QueueCapacity = 1 },
            Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 60000 }
        });
        try
        {
            var a = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            var b = server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training);
            server.Matchmaking.Process();
            var first = await a.WaitAsync();
            var second = await b.WaitAsync();
            server.Matchmaking.Process();
            server.Start();
            Assert.Equal(1, (await server.SnapshotAsync(first.Match.Id)).Tick);
            Assert.Equal(0, (await server.SnapshotAsync(second.Match.Id)).Tick);
            for (var attempt = 0; attempt < 20; attempt++)
            {
                server.Matchmaking.Process();
                var state = await server.SnapshotAsync(second.Match.Id);
                if (state.Tick != 0) { Assert.Equal(1, state.Tick); return; }
            }
            Assert.Fail("Second match did not resume after worker capacity became available.");
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void QueueLimitModesAndRatingBoundsAreValidated()
    {
        var server = new GameServer(Options(new MatchmakingOptions { QueueCapacity = 1, TrainingEnabled = false }));
        try
        {
            Assert.Throws<ArgumentException>(() => server.Matchmaking.Join(Guid.Empty, 1000));
            Assert.Throws<ArgumentException>(() => server.Matchmaking.Join(Guid.NewGuid(), -1));
            Assert.Throws<ArgumentException>(() => server.Matchmaking.Join(Guid.NewGuid(), 5001));
            Assert.Throws<ArgumentException>(() => server.Matchmaking.Join(Guid.NewGuid(), 1000, (MatchKind)42));
            Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(Guid.NewGuid(), 1000, MatchKind.Training));
            var first = server.Matchmaking.Join(Guid.NewGuid(), 0);
            Assert.Throws<InvalidOperationException>(() => server.Matchmaking.Join(Guid.NewGuid(), 5000));
            server.Matchmaking.Cancel(first.PlayerId, first.Id);
            Assert.NotNull(server.Matchmaking.Join(Guid.NewGuid(), 5000));
        }
        finally { server.Stop(); }
    }

    [Theory]
    [InlineData("{\"Matchmaking\":{\"InitialRatingRange\":-1}}")]
    [InlineData("{\"Matchmaking\":{\"MaxRatingRange\":1}}")]
    [InlineData("{\"Matchmaking\":{\"ExpansionIntervalMilliseconds\":0}}")]
    [InlineData("{\"Matchmaking\":{\"BotWaitMilliseconds\":0}}")]
    [InlineData("{\"Matchmaking\":{\"PollIntervalMilliseconds\":0}}")]
    [InlineData("{\"Matchmaking\":{\"RatingRangeStep\":0}}")]
    [InlineData("{\"Matchmaking\":{\"QueueCapacity\":0}}")]
    [InlineData("{\"Matchmaking\":{\"MaxRating\":-1}}")]
    [InlineData("{\"Matchmaking\":{\"SpawnDistance\":0}}")]
    public void InvalidConfigurationIsRejected(string json) => Assert.Throws<ArgumentException>(() => ServerOptions.FromJson(json));

    private static void Send(GameServer server, Guid id, WorldOrder order) => Assert.Equal(DispatchResult.Accepted,
        server.CreateGameplayEndpoint().Accept(new GameMessage(id, "World", JsonSerializer.SerializeToElement(order))));

    private class ReadMatchCommand(TaskCompletionSource<MatchDefinition> completion) : ICommand
    {
        public void Execute() => completion.SetResult(Ioc.Resolve<MatchDefinition>("Match.Definition"));
    }

    private class ManualClock : TimeProvider
    {
        private long _time;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _time);
        public void Advance(int milliseconds) => Interlocked.Add(ref _time, TimeSpan.FromMilliseconds(milliseconds).Ticks);
    }
}
