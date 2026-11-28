namespace SpaceBattle.Lib;

public class MatchmakingOptions
{
    public int InitialRatingRange { get; init; } = 100;
    public int ExpansionIntervalMilliseconds { get; init; } = 30000;
    public int RatingRangeStep { get; init; } = 50;
    public int MaxRatingRange { get; init; } = 1000;
    public int MinRating { get; init; } = 0;
    public int MaxRating { get; init; } = 5000;
    public int BotWaitMilliseconds { get; init; } = 120000;
    public bool AllowDuelBots { get; init; } = true;
    public bool DuelEnabled { get; init; } = true;
    public bool TrainingEnabled { get; init; } = true;
    public int QueueCapacity { get; init; } = 2048;
    public int PollIntervalMilliseconds { get; init; } = 100;
    public int InitialShipFuel { get; init; } = 100;
    public int InitialShipRockets { get; init; } = 5;
    public int SpawnDistance { get; init; } = 30;

    public void Validate()
    {
        if (MinRating < 0 || MaxRating <= MinRating || InitialRatingRange < 0 ||
            MaxRatingRange < InitialRatingRange || MaxRatingRange > (long)MaxRating - MinRating ||
            RatingRangeStep <= 0 || ExpansionIntervalMilliseconds <= 0 || BotWaitMilliseconds <= 0 ||
            QueueCapacity <= 0 || PollIntervalMilliseconds <= 0 || PollIntervalMilliseconds > 60000 ||
            InitialShipFuel <= 0 || InitialShipRockets < 0 || SpawnDistance < 2 || SpawnDistance > 1000000)
            throw new ArgumentException("Invalid matchmaking configuration.");
    }

    public int Range(TimeSpan waiting)
    {
        var steps = Math.Max(0, waiting.TotalMilliseconds) / ExpansionIntervalMilliseconds;
        return (int)Math.Min(MaxRatingRange, InitialRatingRange + Math.Floor(steps) * RatingRangeStep);
    }
}
