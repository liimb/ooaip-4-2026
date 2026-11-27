using System.Text;
using System.Text.Json;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MatchResultsTests
{
    [Fact]
    public async Task VictoryPersistsResultCommandsRatingsAndReplaysActualShots()
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            arena.Send(match.Id, new WorldOrder("Shoot", match.First.ShipId, PlayerId: match.First.Id));
            arena.Send(match.Id, new WorldOrder("Refuel", match.First.ShipId, Amount: -1));
            arena.Send(match.Id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 30, 0)));
            arena.Send(match.Id, new WorldOrder("Tick"));
            var actual = await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.NotNull(result);
            Assert.True(result.Final.Finished);
            Assert.Equal(match.First.Id, result.WinnerId);
            Assert.Equal(match.Second.Id, result.LoserId);
            Assert.Equal(MatchEndReason.FleetDestroyed, result.Reason);
            Assert.Equal(1016, result.FirstRating.After);
            Assert.Equal(984, result.SecondRating.After);
            Assert.True(result.EndedAt >= result.StartedAt);
            var commands = await arena.Server.Results.CommandsAsync(match.Id);
            Assert.Equal(Enumerable.Range(1, commands!.Length).Select(i => (long)i), commands.Select(item => item.Sequence));
            Assert.Contains(commands, item => item.Order.Operation == "Shoot" && item.Order.PlayerId == match.First.Id && item.Error == null);
            Assert.Contains(commands, item => item.Order.Operation == "Refuel" && item.Error == nameof(ArgumentOutOfRangeException));
            Assert.Equal("Finish", commands[^1].Order.Operation);
            var replayed = await arena.Server.Results.ReplayAsync(match.Id);
            AssertState(actual, replayed);
            Assert.Contains(replayed.Objects, item => item.Kind == "Rocket");
            Assert.Equal(SearchState.NotSearching, arena.Server.Matchmaking.Status(match.First.Id).State);
            Assert.Equal(1016, arena.Server.Matchmaking.Join(match.First.Id, 1000).Rating);
            var fresh = new MatchResults(arena.Options.Results, new EloRatingPolicy(32, 0, 5000));
            await fresh.InitializeAsync();
            Assert.Equal(1016, fresh.Rating(match.First.Id, 0));
            Assert.Single(await fresh.HistoryAsync(match.First.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ConcurrentFinishEventsAndRetriesPersistExactlyOneResult()
    {
        var policy = new CountingRatingPolicy();
        var arena = new Arena(rating: policy);
        try
        {
            var match = await arena.CreateAsync();
            arena.Send(match.Id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 30, 0)));
            arena.Send(match.Id, new WorldOrder("Tick"));
            await arena.Server.SnapshotAsync(match.Id);
            var calls = Enumerable.Range(0, 12).Select(_ => arena.Server.FinishMatchAsync(match.Id, MatchEndReason.Interrupted));
            var results = await Task.WhenAll(calls);
            Assert.All(results, result => Assert.Equal(MatchEndReason.FleetDestroyed, result!.Reason));
            Assert.Equal(1, policy.Count);
            Assert.Single(await arena.Server.Results.HistoryAsync(match.First.Id));
            Assert.Single(await arena.Server.Results.ListReplaysAsync());
            var history = await arena.Server.Results.CommandsAsync(match.Id);
            await arena.Server.FinishMatchAsync(match.Id, MatchEndReason.PlayerLeft, match.First.Id);
            Assert.Equal(history, await arena.Server.Results.CommandsAsync(match.Id));
            Assert.Equal(1, policy.Count);
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task DestroyingBothFleetsProducesDraw()
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            arena.Send(match.Id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 0, 0)));
            arena.Send(match.Id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 30, 0)));
            arena.Send(match.Id, new WorldOrder("Tick", PlayerId: match.First.Id));
            await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Null(result!.WinnerId);
            Assert.Null(result.LoserId);
            Assert.Equal(1000, result.FirstRating.After);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Theory]
    [InlineData(MatchEndReason.Interrupted)]
    [InlineData(MatchEndReason.TechnicalError)]
    [InlineData(MatchEndReason.PlayerLeft)]
    [InlineData(MatchEndReason.Disconnected)]
    public async Task NonCompetitiveEndingsDoNotInventWinnerOrChangeRating(MatchEndReason reason)
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            var departed = reason is MatchEndReason.PlayerLeft or MatchEndReason.Disconnected ? match.First.Id : (Guid?)null;
            var result = await arena.Server.FinishMatchAsync(match.Id, reason, departed);
            Assert.Equal(reason, result!.Reason);
            Assert.Equal(departed, result.DepartedPlayerId);
            Assert.Null(result.WinnerId);
            Assert.Equal(result.FirstRating.Before, result.FirstRating.After);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ConfiguredTimeLimitTerminatesThroughRecordedCommand()
    {
        var arena = new Arena(path => new MatchResultsOptions { StoragePath = path, MatchDurationTicks = 1 });
        try
        {
            var match = await arena.CreateAsync();
            arena.Server.Matchmaking.Process();
            await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Equal(MatchEndReason.TimeLimit, result!.Reason);
            Assert.Null(result.WinnerId);
            Assert.Equal(1, result.Final.Tick);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task TrainingVictoryDoesNotCallRatingPolicy()
    {
        var policy = new CountingRatingPolicy();
        var arena = new Arena(rating: policy);
        try
        {
            var match = await arena.CreateAsync(MatchKind.Training);
            arena.Send(match.Id, new WorldOrder("Spawn", Object: new ObjectState(Guid.NewGuid(), "Meteor", 30, 0)));
            arena.Send(match.Id, new WorldOrder("Tick"));
            await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Equal(match.First.Id, result!.WinnerId);
            Assert.False(result.Match.IsRanked);
            Assert.Equal(0, policy.Count);
            Assert.Equal(1000, result.FirstRating.After);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("digest")]
    [InlineData("sequence")]
    [InlineData("tick")]
    [InlineData("command")]
    [InlineData("final")]
    [InlineData("winner")]
    [InlineData("initial")]
    [InlineData("reason")]
    public async Task InvalidReplaysAreRejectedEvenWhenDigestIsRecomputed(string change)
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(match.Id);
            var replay = (await arena.Server.Results.LoadReplayAsync(match.Id))!;
            var commands = replay.Commands.ToArray();
            replay = replay with { Commands = commands };
            switch (change)
            {
                case "version": replay = replay with { Version = 99 }; break;
                case "sequence": commands[0] = commands[0] with { Sequence = 2 }; break;
                case "tick": commands[0] = commands[0] with { Tick = 999 }; break;
                case "command": commands[0] = commands[0] with { Order = new WorldOrder("ExecuteArbitraryCode") }; break;
                case "final": replay = replay with { Result = replay.Result with { Final = replay.Result.Final with { Tick = 999 } } }; break;
                case "winner": replay = replay with { Result = replay.Result with { WinnerId = Guid.NewGuid() } }; break;
                case "initial": replay = replay with { Result = replay.Result with { Initial = replay.Result.Initial with { Version = 42 } } }; break;
                case "reason": replay = replay with { Result = replay.Result with { Reason = MatchEndReason.TechnicalError } }; break;
            }
            replay = replay with { Digest = change == "digest" ? "broken" : ReplayVerifier.Digest(replay) };
            await Assert.ThrowsAsync<InvalidDataException>(() => new ReplayVerifier(arena.Options.Results).PlayAsync(replay));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ParsingEnforcesSizeAndJsonStructure()
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(match.Id);
            var replay = (await arena.Server.Results.LoadReplayAsync(match.Id))!;
            var verifier = new ReplayVerifier(arena.Options.Results);
            Assert.Throws<InvalidDataException>(() => verifier.Parse(Encoding.UTF8.GetBytes("{broken")));
            Assert.Throws<InvalidDataException>(() => verifier.Parse(new byte[arena.Options.Results.MaxReplayBytes + 1]));
            var parsed = verifier.Parse(JsonSerializer.SerializeToUtf8Bytes(replay));
            AssertState(replay.Result.Final, await verifier.PlayAsync(parsed));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task DeleteRemovesOnlyRequestedReplayAndKeepsResultAndCommandHistory()
    {
        var arena = new Arena();
        try
        {
            var first = await arena.CreateAsync();
            var second = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(first.Id);
            await arena.Server.FinishMatchAsync(second.Id);
            Assert.False(await arena.Server.Results.DeleteReplayAsync(Guid.NewGuid()));
            Assert.True(await arena.Server.Results.DeleteReplayAsync(first.Id));
            Assert.False(await arena.Server.Results.DeleteReplayAsync(first.Id));
            Assert.Null(await arena.Server.Results.LoadReplayAsync(first.Id));
            Assert.NotNull(await arena.Server.Results.GetAsync(first.Id));
            Assert.NotEmpty((await arena.Server.Results.CommandsAsync(first.Id))!);
            Assert.Equal(second.Id, Assert.Single(await arena.Server.Results.ListReplaysAsync()).MatchId);
            await arena.Server.FinishMatchAsync(first.Id);
            Assert.Null(await arena.Server.Results.LoadReplayAsync(first.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task StorageFailureRetainsPendingResultAndPlayersUntilSuccessfulRetry()
    {
        var arena = new Arena();
        try
        {
            var match = await arena.CreateAsync();
            Directory.CreateDirectory(arena.Options.Results.StoragePath);
            var failure = await Record.ExceptionAsync(() => arena.Server.FinishMatchAsync(match.Id));
            Assert.True(failure is IOException or UnauthorizedAccessException);
            Assert.Equal(SearchState.InGame, arena.Server.Matchmaking.Status(match.First.Id).State);
            Assert.NotNull(arena.Server.Results.LastError);
            Directory.Delete(arena.Options.Results.StoragePath);
            await arena.Server.Results.FlushAsync();
            Assert.Null(arena.Server.Results.LastError);
            Assert.Single(await arena.Server.Results.HistoryAsync(match.First.Id));
            Assert.Equal(SearchState.NotSearching, arena.Server.Matchmaking.Status(match.First.Id).State);
            await arena.Server.Results.FlushAsync();
            Assert.Single(await arena.Server.Results.HistoryAsync(match.First.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ConcurrentArchiveWritersAndDeletionDoNotLoseResults()
    {
        var arena = new Arena();
        GameServer? other = null;
        try
        {
            other = new GameServer(arena.Options);
            var first = await arena.CreateAsync();
            var a = other.Matchmaking.Join(Guid.NewGuid(), 1200);
            other.Matchmaking.Join(Guid.NewGuid(), 1200);
            other.Matchmaking.Process();
            other.Start();
            var second = (await a.WaitAsync()).Match;
            await Task.WhenAll(arena.Server.FinishMatchAsync(first.Id), other.FinishMatchAsync(second.Id));
            Assert.Equal(2, (await arena.Server.Results.ListReplaysAsync()).Length);
            await Task.WhenAll(arena.Server.Results.DeleteReplayAsync(first.Id), other.Results.DeleteReplayAsync(second.Id));
            Assert.Empty(await arena.Server.Results.ListReplaysAsync());
            Assert.NotNull(await arena.Server.Results.GetAsync(first.Id));
            Assert.NotNull(await other.Results.GetAsync(second.Id));
        }
        finally { other?.Stop(); await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ReplayDoesNotChangeActiveWorldOrItsScope()
    {
        var arena = new Arena();
        try
        {
            var finished = await arena.CreateAsync();
            var active = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(finished.Id);
            var before = await arena.Server.SnapshotAsync(active.Id);
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => arena.Server.Results.ReplayAsync(finished.Id)));
            AssertState(before, await arena.Server.SnapshotAsync(active.Id));
            arena.Send(active.Id, new WorldOrder("Shoot", active.First.ShipId));
            Assert.Contains((await arena.Server.SnapshotAsync(active.Id)).Objects, item => item.Kind == "Rocket");
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task RecordingCanBeDisabledWithoutLosingResults()
    {
        var arena = new Arena(path => new MatchResultsOptions { StoragePath = path, RecordHistory = false, RecordReplays = false });
        try
        {
            var match = await arena.CreateAsync();
            Assert.NotNull(await arena.Server.FinishMatchAsync(match.Id));
            Assert.Null(await arena.Server.Results.CommandsAsync(match.Id));
            Assert.Null(await arena.Server.Results.LoadReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task RecordingLimitTerminatesExplicitlyAndKeepsAValidReplay()
    {
        var arena = new Arena(path => new MatchResultsOptions { StoragePath = path, MaxCommands = 4 });
        try
        {
            var match = await arena.CreateAsync();
            arena.Send(match.Id, new WorldOrder("EngineOff", match.First.ShipId));
            arena.Send(match.Id, new WorldOrder("EngineOff", match.First.ShipId));
            arena.Send(match.Id, new WorldOrder("EngineOff", match.First.ShipId));
            await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Equal(MatchEndReason.RecordingLimit, result!.Reason);
            Assert.Equal(4, (await arena.Server.Results.CommandsAsync(match.Id))!.Length);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task CheckpointRestorePreservesEarlierCommandsAndDeterministicObjectIds()
    {
        var arena = new Arena();
        GameServer? restored = null;
        try
        {
            var match = await arena.CreateAsync();
            arena.Send(match.Id, new WorldOrder("Shoot", match.First.ShipId));
            var checkpoint = await arena.Server.SnapshotAsync(match.Id);
            restored = new GameServer(arena.Options);
            restored.CreateWorld(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(checkpoint))!);
            restored.Start();
            Assert.Equal(DispatchResult.Accepted, restored.CreateGameplayEndpoint().Accept(new GameMessage(match.Id, "World",
                JsonSerializer.SerializeToElement(new WorldOrder("Shoot", match.First.ShipId)))));
            var result = await restored.FinishMatchAsync(match.Id);
            var history = await restored.Results.CommandsAsync(match.Id);
            Assert.Equal(2, history!.Count(item => item.Order.Operation == "Shoot"));
            Assert.Equal(2, result!.Final.Objects.Count(item => item.Kind == "Rocket"));
            AssertState(result.Final, await restored.Results.ReplayAsync(match.Id));
        }
        finally { restored?.Stop(); await arena.CloseAsync(); }
    }

    [Theory]
    [InlineData("{\"Results\":{\"ReplayVersion\":2}}")]
    [InlineData("{\"Results\":{\"RecordHistory\":false}}")]
    [InlineData("{\"Results\":{\"MaxCommands\":0}}")]
    [InlineData("{\"Results\":{\"MaxReplayBytes\":0}}")]
    [InlineData("{\"Results\":{\"VerificationTimeoutMilliseconds\":0}}")]
    [InlineData("{\"Results\":{\"RatingK\":0}}")]
    public void InvalidOptionsAreRejected(string json) => Assert.Throws<ArgumentException>(() => ServerOptions.FromJson(json));

    [Fact]
    public async Task TechnicalFailureInsideCommandIsRecordedAndReplayed()
    {
        var arena = new Arena();
        try
        {
            var match = new MatchDefinition(Guid.NewGuid(), MatchKind.Duel,
                new MatchPlayer(Guid.NewGuid(), 1000, Guid.NewGuid()), new MatchPlayer(Guid.NewGuid(), 1000, Guid.NewGuid()));
            var initial = new WorldSnapshot(1, match.Id, 0, false, new[]
            {
                new ObjectState(match.First.ShipId, "Ship", 0, 0, Owner: match.First.Id),
                new ObjectState(match.Second.ShipId, "Ship", 30, 0, Owner: match.Second.Id)
            }, match, long.MaxValue);
            arena.Server.CreateWorld(initial);
            arena.Send(match.Id, new WorldOrder("Shoot", match.First.ShipId));
            arena.Server.Start();
            await arena.Server.SnapshotAsync(match.Id);
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Equal(MatchEndReason.TechnicalError, result!.Reason);
            Assert.Contains((await arena.Server.Results.CommandsAsync(match.Id))!, entry => entry.Error == nameof(OverflowException));
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ConflictingResultCannotReplaceCommittedMatch()
    {
        var arena = new Arena();
        GameServer? other = null;
        try
        {
            var match = await arena.CreateAsync();
            var checkpoint = await arena.Server.SnapshotAsync(match.Id);
            other = new GameServer(arena.Options);
            other.CreateWorld(checkpoint);
            other.Start();
            var first = await arena.Server.FinishMatchAsync(match.Id);
            Assert.Equal(DispatchResult.Accepted, other.CreateGameplayEndpoint().Accept(new GameMessage(match.Id, "World",
                JsonSerializer.SerializeToElement(new WorldOrder("EngineOn", match.First.ShipId)))));
            await Assert.ThrowsAsync<InvalidDataException>(() => other.FinishMatchAsync(match.Id));
            var saved = await arena.Server.Results.GetAsync(match.Id);
            AssertState(first!.Final, saved!.Final);
            Assert.Single(await arena.Server.Results.HistoryAsync(match.First.Id));
        }
        finally { other?.Stop(); await arena.CloseAsync(); }
    }

    [Fact]
    public async Task ByteLimitIsEnforcedBeforeHistoryCanGrowWithoutBound()
    {
        var arena = new Arena(path => new MatchResultsOptions { StoragePath = path, MaxReplayBytes = 16384 });
        try
        {
            var match = await arena.CreateAsync();
            for (var i = 0; i < 100 && arena.Server.Matchmaking.Outcome(match.Id) == null; i++)
            {
                arena.Send(match.Id, new WorldOrder("EngineOff", match.First.ShipId));
                await arena.Server.SnapshotAsync(match.Id);
            }
            await arena.Server.Results.FlushAsync();
            var result = await arena.Server.Results.GetAsync(match.Id);
            Assert.Equal(MatchEndReason.RecordingLimit, result!.Reason);
            var replay = (await arena.Server.Results.LoadReplayAsync(match.Id))!;
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(replay).Length <= 16384);
            AssertState(result.Final, await arena.Server.Results.ReplayAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task NullArchiveIsNotTreatedAsAnEmptyDatabase()
    {
        var arena = new Arena();
        try
        {
            await File.WriteAllTextAsync(arena.Options.Results.StoragePath, "null");
            await Assert.ThrowsAsync<InvalidDataException>(() => arena.Server.Results.InitializeAsync());
            Assert.Equal("null", await File.ReadAllTextAsync(arena.Options.Results.StoragePath));
        }
        finally { await arena.CloseAsync(); }
    }

    [Fact]
    public async Task CancellationStopsReplayWithoutAffectingStoredResult()
    {
        var arena = new Arena();
        using var cancel = new CancellationTokenSource();
        try
        {
            var match = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(match.Id);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => arena.Server.Results.ReplayAsync(match.Id, cancel.Token));
            Assert.NotNull(await arena.Server.Results.GetAsync(match.Id));
        }
        finally { await arena.CloseAsync(); }
    }

    private static void AssertState(WorldSnapshot expected, WorldSnapshot actual)
    {
        Assert.Equal(expected.GameId, actual.GameId);
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.Finished, actual.Finished);
        Assert.Equal(expected.ObjectSequence, actual.ObjectSequence);
        Assert.Equal(expected.Objects, actual.Objects);
        Assert.Equal(expected.Match, actual.Match);
    }

    [Fact]
    public async Task FinishedMatchReleasesServerCapacityForNextMatch()
    {
        var arena = new Arena(maxGames: 1);
        try
        {
            var first = await arena.CreateAsync();
            await arena.Server.FinishMatchAsync(first.Id);
            var second = await arena.CreateAsync();
            Assert.NotEqual(first.Id, second.Id);
            Assert.NotNull(await arena.Server.Results.GetAsync(first.Id));
            Assert.True((await arena.Server.SnapshotAsync(first.Id)).Finished);
            Assert.False((await arena.Server.SnapshotAsync(second.Id)).Finished);
        }
        finally { await arena.CloseAsync(); }
    }

    private class CountingRatingPolicy : IRatingPolicy
    {
        private int _count;
        public int Count => _count;
        public (int First, int Second) Calculate(MatchResult result, int first, int second)
        {
            Interlocked.Increment(ref _count);
            return (first + 1, second - 1);
        }
    }

    private class Arena
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "SpaceBattle-results-" + Guid.NewGuid());
        public ServerOptions Options { get; }
        public GameServer Server { get; }
        public Arena(Func<string, MatchResultsOptions>? results = null, IRatingPolicy? rating = null, int maxGames = 1024)
        {
            Directory.CreateDirectory(Root);
            var path = Path.Combine(Root, "matches.json");
            Options = new ServerOptions
            {
                MaxGames = maxGames,
                Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 60000 },
                Results = results?.Invoke(path) ?? new MatchResultsOptions { StoragePath = path }
            };
            Server = new GameServer(Options, _ => { }, ratingPolicy: rating);
        }

        public async Task<MatchDefinition> CreateAsync(MatchKind kind = MatchKind.Duel)
        {
            var ticket = Server.Matchmaking.Join(Guid.NewGuid(), 1000, kind);
            if (kind == MatchKind.Duel) Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            Server.Matchmaking.Process();
            var match = (await ticket.WaitAsync()).Match;
            Server.Start();
            await Server.SnapshotAsync(match.Id);
            return match;
        }

        public void Send(Guid id, WorldOrder order) => Assert.Equal(DispatchResult.Accepted,
            Server.CreateGameplayEndpoint().Accept(new GameMessage(id, "World", JsonSerializer.SerializeToElement(order))));

        public async Task CloseAsync()
        {
            await Server.Matchmaking.StopAsync();
            Server.Stop();
            Directory.Delete(Root, true);
        }
    }
}
