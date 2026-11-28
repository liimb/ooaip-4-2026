using App;

namespace SpaceBattle.Lib;

public class CancelMatchmakingCommand(Matchmaker matchmaking, Guid playerId, Guid searchId) : ICommand
{
    private readonly Matchmaker _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    private bool? _result;
    public bool Result => _result ?? throw new InvalidOperationException("Command has not completed successfully.");

    public void Execute()
    {
        _result = null;
        _result = _matchmaking.Cancel(playerId, searchId);
    }
}
