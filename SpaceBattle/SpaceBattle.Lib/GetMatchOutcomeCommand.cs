using App;

namespace SpaceBattle.Lib;

public class GetMatchOutcomeCommand(Matchmaker matchmaking, Guid matchId) : ICommand
{
    private readonly Matchmaker _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
    private MatchOutcome? _result;
    private bool _executed;
    public MatchOutcome? Result => _executed ? _result : throw new InvalidOperationException("Command has not completed successfully.");

    public void Execute()
    {
        _executed = false;
        _result = _matchmaking.Outcome(matchId);
        _executed = true;
    }
}
