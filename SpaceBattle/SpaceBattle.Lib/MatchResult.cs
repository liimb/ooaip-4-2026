namespace SpaceBattle.Lib;

public enum MatchEndReason { FleetDestroyed, Requested, TimeLimit, Interrupted, TechnicalError, PlayerLeft, Disconnected, RecordingLimit }
public record CommandRecord(long Sequence, long Tick, long ElapsedTicks, WorldOrder Order, string? Error);
public record PlayerRating(Guid PlayerId, int Before, int After);
public record MatchResult(Guid MatchId, MatchDefinition Match, WorldSnapshot Initial, WorldSnapshot Final,
    Guid? WinnerId, Guid? LoserId, MatchEndReason Reason, Guid? DepartedPlayerId,
    DateTimeOffset StartedAt, DateTimeOffset EndedAt, PlayerRating FirstRating, PlayerRating SecondRating);
public record ReplayDocument(int Version, GameplayOptions Rules, CommandRecord[] Commands, MatchResult Result, string Digest);
public record ReplayMetadata(Guid MatchId, MatchKind Kind, DateTimeOffset StartedAt, DateTimeOffset EndedAt, int Commands, int Version);
public record MatchProgress(WorldSnapshot Initial, GameplayOptions Rules, DateTimeOffset StartedAt,
    CommandRecord[] Commands, bool CompleteHistory, MatchEndReason? Reason = null, Guid? DepartedPlayerId = null,
    DateTimeOffset? EndedAt = null);
public record MatchArchiveEntry(MatchResult Result, CommandRecord[]? History, ReplayDocument? Replay);
public record MatchArchive(int Version, MatchArchiveEntry[] Entries);
