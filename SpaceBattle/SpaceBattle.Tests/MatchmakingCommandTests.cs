using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MatchmakingCommandTests
{
    [Fact]
    public async Task CommandsAutomaticallyMatchAndNotifyWithoutWebApplication()
    {
        var server = new GameServer(new ServerOptions
        {
            Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 10 }
        });
        try
        {
            var first = new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000);
            var second = new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000);
            ICommand operation = first;
            operation.Execute();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var wait = new WaitForMatchCommand(server.Matchmaking, first.Result.PlayerId, first.Result.Id, deadline.Token);
            wait.Execute();
            Assert.False(wait.Result.IsCompleted);
            second.Execute();
            server.Start();
            var found = await wait.Result;
            Assert.Equal(first.Result.PlayerId, found.Match.First.Id);
            Assert.Equal(second.Result.PlayerId, found.Match.Second.Id);
            var state = new GetMatchmakingStatusCommand(server.Matchmaking, second.Result.PlayerId);
            state.Execute();
            Assert.Equal(SearchState.InGame, state.Result.State);
            Assert.Equal(found.Match.Id, state.Result.Match!.Match.Id);
            Assert.Throws<InvalidOperationException>(() =>
                new JoinMatchmakingCommand(server.Matchmaking, first.Result.PlayerId, 1000, MatchKind.Training).Execute());
            var lateCancel = new CancelMatchmakingCommand(server.Matchmaking, first.Result.PlayerId, first.Result.Id);
            lateCancel.Execute();
            Assert.False(lateCancel.Result);
            Assert.Equal(2, (await server.SnapshotAsync(found.Match.Id)).Objects.Length);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task CancellationCompletesWaitAndOldTicketCannotCancelNewSearch()
    {
        var server = new GameServer(new ServerOptions());
        try
        {
            var player = Guid.NewGuid();
            var invalid = new JoinMatchmakingCommand(server.Matchmaking, player, -1);
            Assert.Throws<ArgumentException>(() => invalid.Execute());
            var join = new JoinMatchmakingCommand(server.Matchmaking, player, 1000);
            join.Execute();
            var wait = new WaitForMatchCommand(server.Matchmaking, player, join.Result.Id);
            wait.Execute();
            var cancel = new CancelMatchmakingCommand(server.Matchmaking, player, join.Result.Id);
            cancel.Execute();
            Assert.True(cancel.Result);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.Result);
            var next = new JoinMatchmakingCommand(server.Matchmaking, player, 1000);
            next.Execute();
            cancel.Execute();
            Assert.False(cancel.Result);
            var status = new GetMatchmakingStatusCommand(server.Matchmaking, player);
            status.Execute();
            Assert.Equal(next.Result.Id, status.Result.SearchId);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task CancellingWaitDoesNotCancelSearchAndNotificationCanBeRequestedAfterMatch()
    {
        var server = new GameServer(new ServerOptions());
        try
        {
            var first = new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000);
            first.Execute();
            using var cancellation = new CancellationTokenSource();
            var wait = new WaitForMatchCommand(server.Matchmaking, first.Result.PlayerId, first.Result.Id, cancellation.Token);
            wait.Execute();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.Result);
            var status = new GetMatchmakingStatusCommand(server.Matchmaking, first.Result.PlayerId);
            status.Execute();
            Assert.Equal(SearchState.Searching, status.Result.State);
            new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000).Execute();
            server.Matchmaking.Process();
            var lateWait = new WaitForMatchCommand(server.Matchmaking, first.Result.PlayerId, first.Result.Id);
            lateWait.Execute();
            Assert.Equal(await first.Result.WaitAsync(), await lateWait.Result);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public async Task SnapshotRestorePreservesMembershipAndFinishedMatchAllowsRejoiningWithoutHttp()
    {
        var path = Path.Combine(Path.GetTempPath(), "matchmaking-" + Guid.NewGuid() + ".json");
        var options = new ServerOptions
        {
            Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 60000 }
        };
        var server = new GameServer(options);
        try
        {
            var first = new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000);
            first.Execute();
            new JoinMatchmakingCommand(server.Matchmaking, Guid.NewGuid(), 1000).Execute();
            server.Matchmaking.Process();
            var found = await first.Result.WaitAsync();
            server.Start();
            var snapshot = await server.SnapshotAsync(found.Match.Id);
            var store = new SnapshotStore(path);
            await store.SaveAsync(new[] { snapshot });
            server.Stop();
            server = new GameServer(options);
            server.CreateWorld(Assert.Single(await store.LoadAsync()));
            var status = new GetMatchmakingStatusCommand(server.Matchmaking, first.Result.PlayerId);
            status.Execute();
            Assert.Equal(found.Match, status.Result.Match!.Match);
            Assert.Throws<InvalidOperationException>(() =>
                new JoinMatchmakingCommand(server.Matchmaking, first.Result.PlayerId, 1000).Execute());
            server.Stop();
            await store.SaveAsync(new[] { snapshot with { Finished = true } });
            server = new GameServer(options);
            server.CreateWorld(Assert.Single(await store.LoadAsync()));
            status = new GetMatchmakingStatusCommand(server.Matchmaking, first.Result.PlayerId);
            status.Execute();
            Assert.Equal(SearchState.NotSearching, status.Result.State);
            var again = new JoinMatchmakingCommand(server.Matchmaking, first.Result.PlayerId, 1000);
            again.Execute();
            Assert.NotEqual(first.Result.Id, again.Result.Id);
        }
        finally
        {
            server.Stop();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ConstructingCommandsHasNoSideEffectsAndResultsRequireExecution()
    {
        var server = new GameServer(new ServerOptions());
        try
        {
            var player = Guid.NewGuid();
            var join = new JoinMatchmakingCommand(server.Matchmaking, player, 1000);
            var cancel = new CancelMatchmakingCommand(server.Matchmaking, player, Guid.NewGuid());
            var wait = new WaitForMatchCommand(server.Matchmaking, player, Guid.NewGuid());
            var status = new GetMatchmakingStatusCommand(server.Matchmaking, player);
            var outcome = new GetMatchOutcomeCommand(server.Matchmaking, Guid.NewGuid());
            Assert.Throws<InvalidOperationException>(() => join.Result);
            Assert.Throws<InvalidOperationException>(() => cancel.Result);
            Assert.Throws<InvalidOperationException>(() => { _ = wait.Result; });
            Assert.Throws<InvalidOperationException>(() => status.Result);
            Assert.Throws<InvalidOperationException>(() => outcome.Result);
            status.Execute();
            Assert.Equal(SearchState.NotSearching, status.Result.State);
            outcome.Execute();
            Assert.Null(outcome.Result);
            Assert.Throws<KeyNotFoundException>(() => wait.Execute());
            join.Execute();
            Assert.Throws<InvalidOperationException>(() => join.Execute());
            Assert.Throws<InvalidOperationException>(() => join.Result);
        }
        finally { server.Stop(); }
    }

    [Fact]
    public void CommandsRejectMissingMatchmaker()
    {
        Assert.Throws<ArgumentNullException>(() => new JoinMatchmakingCommand(null!, Guid.NewGuid(), 1000));
        Assert.Throws<ArgumentNullException>(() => new CancelMatchmakingCommand(null!, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentNullException>(() => new GetMatchmakingStatusCommand(null!, Guid.NewGuid()));
        Assert.Throws<ArgumentNullException>(() => new WaitForMatchCommand(null!, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentNullException>(() => new GetMatchOutcomeCommand(null!, Guid.NewGuid()));
    }
}
