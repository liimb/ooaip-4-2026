using App;

namespace SpaceBattle.Lib;

public class WaitForMatchCommand(Matchmaker matchmaking, Guid playerId, Guid searchId, CancellationToken token = default) : ICommand
{
    private readonly Matchmaker _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    private Task<MatchFound>? _result;
    public Task<MatchFound> Result => _result ?? throw new InvalidOperationException("Command has not completed successfully.");

    public void Execute()
    {
        _result = null;
        _result = _matchmaking.WaitAsync(playerId, searchId, token);
    }
}
