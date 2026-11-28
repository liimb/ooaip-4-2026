using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public class WaitForMatchEndpoint
{
    private readonly Matchmaker _matchmaking;

    public WaitForMatchEndpoint(Matchmaker matchmaking)
    {
        _matchmaking = matchmaking
            ?? throw new ArgumentNullException(nameof(matchmaking));
    }

    public async Task<IResult> HandleAsync(
        Guid playerId,
        Guid searchId,
        CancellationToken token)
    {
        try
        {
            var command = new WaitForMatchCommand(
                _matchmaking,
                playerId,
                searchId,
                token);

            command.Execute();

            return Results.Ok(await command.Result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (OperationCanceledException)
        {
            return Results.NoContent();
        }
    }
}
