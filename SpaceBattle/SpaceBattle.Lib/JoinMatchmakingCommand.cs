using App;

namespace SpaceBattle.Lib;

public class JoinMatchmakingCommand(Matchmaker matchmaking, Guid playerId, int rating, MatchKind kind = MatchKind.Duel) : ICommand
{
    private readonly Matchmaker _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    private SearchTicket? _result;
    public SearchTicket Result => _result ?? throw new InvalidOperationException("Command has not completed successfully.");

    public void Execute()
    {
        _result = null;
        _result = _matchmaking.Join(playerId, rating, kind);
    }
}
