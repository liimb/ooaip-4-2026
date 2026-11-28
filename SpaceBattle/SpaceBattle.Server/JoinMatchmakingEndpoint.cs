using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class JoinMatchmakingEndpoint
{
    private readonly WebApplication _app;
    private readonly Matchmaker _matchmaking;

    public JoinMatchmakingEndpoint(WebApplication app, Matchmaker matchmaking)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public void Map() => _app.MapPost("/matchmaking", Handle);

    private IResult Handle(SearchRequest request)
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
    }
}
