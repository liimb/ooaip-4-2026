namespace SpaceBattle.Lib;

public interface IRatingPolicy
{
    (int First, int Second) Calculate(MatchResult result, int first, int second);
}

public class EloRatingPolicy(int k, int minimum, int maximum) : IRatingPolicy
{
    public (int First, int Second) Calculate(MatchResult result, int first, int second)
    {
        if (!result.Match.IsRanked || result.Reason is not (MatchEndReason.FleetDestroyed or MatchEndReason.TimeLimit))
            return (first, second);
        var score = result.WinnerId == null ? 0.5 : result.WinnerId == result.Match.First.Id ? 1.0 : 0.0;
        var expected = 1.0 / (1.0 + Math.Pow(10, ((double)second - first) / 400));
        var change = (int)Math.Round(k * (score - expected), MidpointRounding.AwayFromZero);
        return ((int)Math.Clamp((long)first + change, minimum, maximum),
            (int)Math.Clamp((long)second - change, minimum, maximum));
    }
}
