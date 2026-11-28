using SpaceBattle.Lib;

namespace SpaceBattle.Server;

public record SearchRequest(Guid PlayerId, int Rating, MatchKind Kind = MatchKind.Duel);
