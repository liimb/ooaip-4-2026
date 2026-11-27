using System.Net;
using System.Net.Http.Json;
using SpaceBattle.Lib;
using SpaceBattle.Server;

namespace SpaceBattle.Tests;

public class MatchResultEndpointTests
{
    [Fact]
    public async Task HttpResultsHistoryMetadataVerificationPlaybackAndDeletionWorkTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "SpaceBattle-result-api-" + Guid.NewGuid());
        var manager = new ServerManager(Options(root));
        try
        {
            await manager.StartAsync();
            var first = manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Process();
            var match = (await first.WaitAsync()).Match;
            await manager.Server.FinishMatchAsync(match.Id);
            using var client = new HttpClient { BaseAddress = new Uri(manager.Address!), Timeout = TimeSpan.FromSeconds(10) };
            var result = await client.GetFromJsonAsync<MatchResult>($"/matches/{match.Id}");
            Assert.Equal(match, result!.Match);
            Assert.Single((await client.GetFromJsonAsync<MatchResult[]>($"/players/{first.PlayerId}/matches"))!);
            Assert.NotEmpty((await client.GetFromJsonAsync<CommandRecord[]>($"/matches/{match.Id}/commands"))!);
            Assert.Single((await client.GetFromJsonAsync<ReplayMetadata[]>("/replays"))!);
            Assert.Equal(match.Id, (await client.GetFromJsonAsync<ReplayMetadata>($"/replays/{match.Id}/metadata"))!.MatchId);
            Assert.NotNull(await client.GetFromJsonAsync<ReplayDocument>($"/replays/{match.Id}"));
            using var verification = await client.PostAsync($"/replays/{match.Id}/verify", null);
            Assert.Equal(HttpStatusCode.OK, verification.StatusCode);
            using var playback = await client.PostAsync($"/replays/{match.Id}/play", null);
            Assert.Equal(HttpStatusCode.OK, playback.StatusCode);
            Assert.True((await playback.Content.ReadFromJsonAsync<WorldSnapshot>())!.Finished);
            using var deleted = await client.DeleteAsync($"/replays/{match.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var missing = await client.DeleteAsync($"/replays/{match.Id}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.NotNull(await client.GetFromJsonAsync<MatchResult>($"/matches/{match.Id}"));
            using var unknown = await client.GetAsync($"/matches/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await manager.HardStopAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task HardStopRecordsInterruptionAndStaleCheckpointCannotRestartCommittedMatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "SpaceBattle-result-stop-" + Guid.NewGuid());
        var options = Options(root);
        var manager = new ServerManager(options);
        try
        {
            await manager.StartAsync();
            var ticket = manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Join(Guid.NewGuid(), 1000);
            manager.Server.Matchmaking.Process();
            var match = (await ticket.WaitAsync()).Match;
            var checkpoint = await manager.Server.SnapshotAsync(match.Id);
            await new SnapshotStore(options.Lifecycle.SnapshotPath).SaveAsync(new[] { checkpoint });
            await manager.HardStopAsync();
            var result = await manager.Server.Results.GetAsync(match.Id);
            Assert.Equal(MatchEndReason.Interrupted, result!.Reason);
            await manager.StartAsync();
            Assert.Equal(SearchState.NotSearching, manager.Server.Matchmaking.Status(ticket.PlayerId).State);
            Assert.NotNull(manager.Server.Matchmaking.Join(ticket.PlayerId, 1000));
            Assert.Single(await manager.Server.Results.HistoryAsync(ticket.PlayerId));
        }
        finally
        {
            await manager.HardStopAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static ServerOptions Options(string root) => new()
    {
        Endpoint = new EndpointOptions { Url = "http://127.0.0.1:0" },
        Lifecycle = new LifecycleOptions { SnapshotPath = Path.Combine(root, "server.json") },
        Results = new MatchResultsOptions { StoragePath = Path.Combine(root, "matches.json") },
        Matchmaking = new MatchmakingOptions { PollIntervalMilliseconds = 60000 }
    };
}
