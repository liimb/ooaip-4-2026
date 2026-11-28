using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Tests;

public class MatchmakingEndpointTests
{
    [Fact]
    public async Task HttpSearchUsesSameMatchmakerAsCommandsAndNotifiesBothPlayers()
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
            var player = Guid.NewGuid();
            using var joined = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            Assert.Equal(HttpStatusCode.Accepted, joined.StatusCode);
            var searchId = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetGuid();
            var waiting = client.GetFromJsonAsync<MatchFound>($"/matchmaking/{player}/{searchId}/wait");
            var opponent = new JoinMatchmakingCommand(manager.Server.Matchmaking, Guid.NewGuid(), 1000);
            opponent.Execute();
            var found = await waiting;
            Assert.NotNull(found);
            Assert.Equal(player, found.Match.First.Id);
            Assert.Equal(opponent.Result.PlayerId, found.Match.Second.Id);
            Assert.Equal(found, await opponent.Result.WaitAsync());
            var status = await client.GetFromJsonAsync<SearchStatus>($"/matchmaking/{player}");
            Assert.Equal(SearchState.InGame, status!.State);
            Assert.Equal(found, status.Match);
            using var duplicate = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var lateCancel = await client.DeleteAsync($"/matchmaking/{player}/{searchId}");
            Assert.Equal(HttpStatusCode.Conflict, lateCancel.StatusCode);
            Assert.Equal(found, await client.GetFromJsonAsync<MatchFound>($"/matchmaking/{player}/{searchId}/wait"));
        }
        finally { await manager.HardStopAsync(); }
    }

    [Fact]
    public async Task HttpCancellationReleasesPlayerAndRejectsOldSearchIdentifier()
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
            using var invalid = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, -1));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var joined = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            Assert.Equal(HttpStatusCode.Accepted, joined.StatusCode);
            var id = (await joined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetGuid();
            var waiting = client.GetAsync($"/matchmaking/{player}/{id}/wait");
            using var cancelled = await client.DeleteAsync($"/matchmaking/{player}/{id}");
            Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
            using var notification = await waiting;
            Assert.Contains(notification.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
            var status = await client.GetFromJsonAsync<SearchStatus>($"/matchmaking/{player}");
            Assert.Equal(SearchState.NotSearching, status!.State);
            using var rejoined = await client.PostAsJsonAsync("/matchmaking", new SearchRequest(player, 1000));
            Assert.Equal(HttpStatusCode.Accepted, rejoined.StatusCode);
            var nextId = (await rejoined.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetGuid();
            Assert.NotEqual(id, nextId);
            using var oldCancel = await client.DeleteAsync($"/matchmaking/{player}/{id}");
            Assert.Equal(HttpStatusCode.Conflict, oldCancel.StatusCode);
            status = await client.GetFromJsonAsync<SearchStatus>($"/matchmaking/{player}");
            Assert.Equal(nextId, status!.SearchId);
            using var missing = await client.GetAsync($"/matchmaking/{player}/{id}/wait");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally { await manager.HardStopAsync(); }
    }
}
