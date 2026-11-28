using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class WaitForMatchEndpoint
{
    private readonly WebApplication _app;
    private readonly Matchmaker _matchmaking;

    public WaitForMatchEndpoint(WebApplication app, Matchmaker matchmaking)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public void Map() => _app.MapGet("/matchmaking/{playerId:guid}/{searchId:guid}/wait", HandleAsync);

    private async Task<IResult> HandleAsync(Guid playerId, Guid searchId, CancellationToken token)
    {
        try
        {
            var command = new WaitForMatchCommand(_matchmaking, playerId, searchId, token);
            command.Execute();
            return Results.Ok(await command.Result);
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (OperationCanceledException) { return Results.NoContent(); }
    }
}
