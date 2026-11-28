using App;

namespace SpaceBattle.Lib;

public class GetMatchmakingStatusCommand(Matchmaker matchmaking, Guid playerId) : ICommand
{
    private readonly Matchmaker _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    private SearchStatus? _result;
    public SearchStatus Result => _result ?? throw new InvalidOperationException("Command has not completed successfully.");

    public void Execute()
    {
        _result = null;
        _result = _matchmaking.Status(playerId);
    }
}
