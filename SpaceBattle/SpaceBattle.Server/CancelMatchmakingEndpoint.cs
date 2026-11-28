using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class CancelMatchmakingEndpoint
{
    private readonly Matchmaker _matchmaking;

    public CancelMatchmakingEndpoint(Matchmaker matchmaking)
    {
        _matchmaking = matchmaking
            ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public IResult Handle(Guid playerId, Guid searchId)
    {
        var command = new CancelMatchmakingCommand(
            _matchmaking,
            playerId,
            searchId);

        command.Execute();

        return command.Result
            ? Results.NoContent()
            : Results.Conflict();
    }
}
