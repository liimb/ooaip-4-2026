using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class CancelMatchmakingEndpoint
{
    private readonly WebApplication _app;
    private readonly Matchmaker _matchmaking;

    public CancelMatchmakingEndpoint(WebApplication app, Matchmaker matchmaking)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public void Map() => _app.MapDelete("/matchmaking/{playerId:guid}/{searchId:guid}", Handle);

    private IResult Handle(Guid playerId, Guid searchId)
    {
        var command = new CancelMatchmakingCommand(_matchmaking, playerId, searchId);
        command.Execute();
        return command.Result ? Results.NoContent() : Results.Conflict();
    }
}
