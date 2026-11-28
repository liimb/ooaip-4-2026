using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class GetMatchmakingStatusEndpoint
{
    private readonly Matchmaker _matchmaking;

    public GetMatchmakingStatusEndpoint(Matchmaker matchmaking)
    {
        _matchmaking = matchmaking
            ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public IResult Handle(Guid playerId)
    {
        var command = new GetMatchmakingStatusCommand(
            _matchmaking,
            playerId);

        command.Execute();

        return Results.Ok(command.Result);
    }
}
