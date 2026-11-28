using SpaceBattle.Lib;

namespace SpaceBattle.Server;

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
        new JoinMatchmakingEndpoint(_app, _matchmaking).Map();
        new GetMatchmakingStatusEndpoint(_app, _matchmaking).Map();
        new CancelMatchmakingEndpoint(_app, _matchmaking).Map();
        new WaitForMatchEndpoint(_app, _matchmaking).Map();
    }
}
