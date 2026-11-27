namespace SpaceBattle.Lib;

public class MatchResultsOptions
{
    public string StoragePath { get; init; } = "snapshots/matches.json";
    public bool RecordHistory { get; init; } = true;
    public bool RecordReplays { get; init; } = true;
    public int ReplayVersion { get; init; } = 1;
    public int MaxReplayBytes { get; init; } = 16777216;
    public int MaxCommands { get; init; } = 100000;
    public int VerificationTimeoutMilliseconds { get; init; } = 10000;
    public long MatchDurationTicks { get; init; } = 0;
    public int RatingK { get; init; } = 32;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(StoragePath);
        if (RecordReplays && !RecordHistory || ReplayVersion != 1 || MaxReplayBytes < 16384 ||
            MaxReplayBytes > 268435456 || MaxCommands < 4 || MaxCommands > 1000000 ||
            VerificationTimeoutMilliseconds <= 0 || VerificationTimeoutMilliseconds > 300000 ||
            MatchDurationTicks < 0 || RatingK <= 0 || RatingK > 1000)
            throw new ArgumentException("Invalid match result settings.");
    }
}
