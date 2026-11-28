using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class GetMatchmakingStatusEndpoint
{
    private readonly WebApplication _app;
    private readonly Matchmaker _matchmaking;

    public GetMatchmakingStatusEndpoint(WebApplication app, Matchmaker matchmaking)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public void Map() => _app.MapGet("/matchmaking/{playerId:guid}", Handle);

    private IResult Handle(Guid playerId)
    {
        var command = new GetMatchmakingStatusCommand(_matchmaking, playerId);
        command.Execute();
        return Results.Ok(command.Result);
    }
}
