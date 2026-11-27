using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class MatchResultEndpoints
{
    public static void Map(WebApplication app, MatchResults results)
    {
        app.MapGet("/matches/{id:guid}", async (Guid id, CancellationToken token) =>
            await results.GetAsync(id, token) is { } result ? Results.Ok(result) : Results.NotFound());
        app.MapGet("/players/{id:guid}/matches", async (Guid id, CancellationToken token) =>
            Results.Ok(await results.HistoryAsync(id, token)));
        app.MapGet("/matches/{id:guid}/commands", async (Guid id, CancellationToken token) =>
            await results.CommandsAsync(id, token) is { } history ? Results.Ok(history) : Results.NotFound());
        app.MapGet("/replays", async (CancellationToken token) => Results.Ok(await results.ListReplaysAsync(token)));
        app.MapGet("/replays/{id:guid}", async (Guid id, CancellationToken token) =>
            await results.LoadReplayAsync(id, token) is { } replay ? Results.Ok(replay) : Results.NotFound());
        app.MapGet("/replays/{id:guid}/metadata", async (Guid id, CancellationToken token) =>
            (await results.ListReplaysAsync(token)).SingleOrDefault(item => item.MatchId == id) is { } metadata
                ? Results.Ok(metadata) : Results.NotFound());
        app.MapPost("/replays/{id:guid}/play", async (Guid id, CancellationToken token) =>
        {
            try { return Results.Ok(await results.ReplayAsync(id, token)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidDataException error) { return Results.BadRequest(new { error.Message }); }
        });
        app.MapPost("/replays/{id:guid}/verify", async (Guid id, CancellationToken token) =>
        {
            try
            {
                await results.ReplayAsync(id, token);
                return Results.Ok(new { Valid = true });
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (InvalidDataException error) { return Results.BadRequest(new { error.Message }); }
        });
        app.MapDelete("/replays/{id:guid}", async (Guid id, CancellationToken token) =>
            await results.DeleteReplayAsync(id, token) ? Results.NoContent() : Results.NotFound());
    }
}
