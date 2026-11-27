using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public record SearchRequest(Guid PlayerId, int Rating, MatchKind Kind = MatchKind.Duel);

public class MatchmakingEndpoints
{
    public static void Map(WebApplication app, Matchmaker matchmaking)
    {
        app.MapPost("/matchmaking", (SearchRequest request) =>
        {
            try
            {
                var ticket = matchmaking.Join(request.PlayerId, request.Rating, request.Kind);
                return Results.Json(new { SearchId = ticket.Id, ticket.PlayerId }, statusCode: 202);
            }
            catch (ArgumentException error) { return Results.BadRequest(new { error.Message }); }
            catch (InvalidOperationException error) { return Results.Conflict(new { error.Message }); }
        });
        app.MapGet("/matchmaking/{playerId:guid}", (Guid playerId) => Results.Ok(matchmaking.Status(playerId)));
        app.MapDelete("/matchmaking/{playerId:guid}/{searchId:guid}", (Guid playerId, Guid searchId) =>
            matchmaking.Cancel(playerId, searchId) ? Results.NoContent() : Results.Conflict());
        app.MapGet("/matchmaking/{playerId:guid}/{searchId:guid}/wait", async (Guid playerId, Guid searchId, CancellationToken token) =>
        {
            try { return Results.Ok(await matchmaking.WaitAsync(playerId, searchId, token)); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (OperationCanceledException) { return Results.NoContent(); }
        });
    }
}
