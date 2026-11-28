using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class JoinMatchmakingEndpoint
{
    private readonly Matchmaker _matchmaking;

    public JoinMatchmakingEndpoint(Matchmaker matchmaking)
    {
        _matchmaking = matchmaking
            ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public IResult Handle(SearchRequest request)
    {
        try
        {
            var command = new JoinMatchmakingCommand(
                _matchmaking,
                request.PlayerId,
                request.Rating,
                request.Kind);

            command.Execute();

            var ticket = command.Result;

            return Results.Json(
                new
                {
                    SearchId = ticket.Id,
                    ticket.PlayerId
                },
                statusCode: 202);
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Results.Conflict(new { error.Message });
        }
    }
}
