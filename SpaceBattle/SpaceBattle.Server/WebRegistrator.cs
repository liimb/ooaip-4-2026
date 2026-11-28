using SpaceBattle.Lib;


namespace SpaceBattle.Server;

public static class WebRegistrator
{
    public static void Register(
        WebApplication app,
        Matchmaker matchmaking)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(matchmaking);

        RegisterMatchmaking(app, matchmaking);
    }

    private static void RegisterMatchmaking(
        WebApplication app,
        Matchmaker matchmaking)
    {
        var join = new JoinMatchmakingEndpoint(matchmaking);
        var status = new GetMatchmakingStatusEndpoint(matchmaking);
        var cancel = new CancelMatchmakingEndpoint(matchmaking);
        var wait = new WaitForMatchEndpoint(matchmaking);

        app.MapPost(
            "/matchmaking",
            join.Handle);

        app.MapGet(
            "/matchmaking/{playerId:guid}",
            status.Handle);

        app.MapDelete(
            "/matchmaking/{playerId:guid}/{searchId:guid}",
            cancel.Handle);

        app.MapGet(
            "/matchmaking/{playerId:guid}/{searchId:guid}/wait",
            wait.HandleAsync);
    }
}
