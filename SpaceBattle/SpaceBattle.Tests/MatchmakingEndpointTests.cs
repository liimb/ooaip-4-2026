using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Tests;

public class MatchmakingEndpointTests
{
    [Fact]
    public async Task HttpSearchAutomaticallyMatchesNotifiesAndRejectsDuplicatePlayer()
    {
        var manager = new ServerManager(new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { RestoreOnStart = false, SaveOnSoftStop = false },
            Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 10 }
        });
        try
        {
            await manager.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(manager.Address!), Timeout = TimeSpan.FromSeconds(10) };
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            using var joined = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(first, 1000));
            Assert.Equal(HttpStatusCode.Accepted, joined.StatusCode);
            var searchId = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetGuid();
            var notification = client.GetFromJsonAsync<MatchFound>($"/matchmaking/{first}/{searchId}/wait");
            using var opponent = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(second, 1000));
            Assert.Equal(HttpStatusCode.Accepted, opponent.StatusCode);
            var found = await notification;
            Assert.NotNull(found);
            Assert.Equal(first, found.Match.First.Id);
            Assert.Equal(second, found.Match.Second.Id);
            var state = await client.GetFromJsonAsync<SearchStatus>($"/matchmaking/{second}");
            Assert.Equal(SearchState.InGame, state!.State);
            Assert.Equal(found.Match.Id, state.Match!.Match.Id);
            using var duplicate = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(first, 1000, MatchKind.Training));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var lateCancel = await client.DeleteAsync($"/matchmaking/{first}/{searchId}");
            Assert.Equal(HttpStatusCode.Conflict, lateCancel.StatusCode);
            Assert.Equal(2, (await manager.Server.SnapshotAsync(found.Match.Id)).Objects.Length);
        }
        finally { await manager.HardStopAsync(); }
    }

    [Fact]
    public async Task HttpCancellationCompletesWaitAndAllowsRejoining()
    {
        var manager = new ServerManager(new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { RestoreOnStart = false, SaveOnSoftStop = false }
        });
        try
        {
            await manager.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(manager.Address!), Timeout = TimeSpan.FromSeconds(10) };
            var player = Guid.NewGuid();
            using var bad = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, -1));
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            using var joined = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            var id = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetGuid();
            var waiting = client.GetAsync($"/matchmaking/{player}/{id}/wait");
            using var cancelled = await client.DeleteAsync($"/matchmaking/{player}/{id}");
            Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
            using var response = await waiting;
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
            using var again = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        }
        finally { await manager.HardStopAsync(); }
    }

    [Fact]
    public async Task RestartRestoresMatchMembershipAndFinishedHistoryDoesNotBlockPlayer()
    {
        var path = Path.Combine(Path.GetTempPath(), "matchmaking-" + Guid.NewGuid() + ".json");
        var options = new ServerOptions
        {
            Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
            Lifecycle = new LifecycleOptions { SnapshotPath = path },
            Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 60000 }
        };
        var manager = new ServerManager(options);
        try
        {
            await manager.StartAsync();
            var first = manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Process();
            var found = await first.WaitAsync();
            await manager.RestartAsync();
            Assert.Equal(found.Match, manager.Server.Matchmaking.Status(first.PlayerId).Match!.Match);
            Assert.Throws<InvalidOperationException>(() => manager.Server.Matchmaking.Join(first.PlayerId, 1000));
            var snapshot = await manager.Server.SnapshotAsync(found.Match.Id);
            await manager.HardStopAsync();
            await new SnapshotStore(path).SaveAsync(new[] { snapshot with { Finished = true } });
            await manager.StartAsync();
            Assert.Equal(SearchState.NotSearching, manager.Server.Matchmaking.Status(first.PlayerId).State);
            Assert.NotNull(manager.Server.Matchmaking.Join(first.PlayerId, 1000));
        }
        finally
        {
            await manager.HardStopAsync();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
