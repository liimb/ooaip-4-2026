using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public record SearchRequest(Guid PlayerId, int Rating, MatchKind Kind = MatchKind.Duel);

public class MatchmakingEndpoints
{
    private readonly WebApplication _app;
    private readonly Matchmaker _matchmaking;

    public MatchmakingEndpoints(WebApplication app, Matchmaker matchmaking)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public void Map()
    {
        _app.MapPost("/matchmaking", (SearchRequest request) =>
        {
            try
            {
                var command = new JoinMatchmakingCommand(_matchmaking, request.PlayerId, request.Rating, request.Kind);
                command.Execute();
                var ticket = command.Result;
                return Results.Json(new { SearchId = ticket.Id, ticket.PlayerId }, statusCode: 202);
            }
            catch (ArgumentException error) { return Results.BadRequest(new { error.Message }); }
            catch (InvalidOperationException error) { return Results.Conflict(new { error.Message }); }
        });
        _app.MapGet("/matchmaking/{playerId:guid}", (Guid playerId) =>
        {
            var command = new GetMatchmakingStatusCommand(_matchmaking, playerId);
            command.Execute();
            return Results.Ok(command.Result);
        });
        _app.MapDelete("/matchmaking/{playerId:guid}/{searchId:guid}", (Guid playerId, Guid searchId) =>
        {
            var command = new CancelMatchmakingCommand(_matchmaking, playerId, searchId);
            command.Execute();
            return command.Result ? Results.NoContent() : Results.Conflict();
        });
        _app.MapGet("/matchmaking/{playerId:guid}/{searchId:guid}/wait", async (Guid playerId, Guid searchId, CancellationToken token) =>
        {
            try
            {
                var command = new WaitForMatchCommand(_matchmaking, playerId, searchId, token);
                command.Execute();
                return Results.Ok(await command.Result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (OperationCanceledException) { return Results.NoContent(); }
        });
    }
}
