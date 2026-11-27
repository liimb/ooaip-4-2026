using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using App;

namespace SpaceBattle.Lib;

public class ReplayVerifier
{
    private readonly MatchResultsOptions _options;

    public ReplayVerifier(MatchResultsOptions options)
    {
        options.Validate();
        _options = options;
    }

    public static string Digest(ReplayDocument replay) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(replay with { Digest = string.Empty })));

    public ReplayDocument Parse(byte[] data)
    {
        if (data.Length > _options.MaxReplayBytes) throw new InvalidDataException("Replay exceeds size limit.");
        try
        {
            return JsonSerializer.Deserialize<ReplayDocument>(data, new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                MaxDepth = 32
            }) ?? throw new InvalidDataException("Replay is required.");
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed replay.", error); }
    }

    public async Task<WorldSnapshot> PlayAsync(ReplayDocument replay, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (replay == null || replay.Commands == null || replay.Commands.Length > _options.MaxCommands)
            throw new InvalidDataException("Invalid replay.");
        var copy = Parse(JsonSerializer.SerializeToUtf8Bytes(replay));
        return await Task.Run(() => Play(copy, token), token);
    }

    private WorldSnapshot Play(ReplayDocument replay, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_options.VerificationTimeoutMilliseconds);
        Validate(replay);
        var scope = new GameScope();
        var owner = Environment.CurrentManagedThreadId;
        var world = new GameWorld(replay.Result.Initial, replay.Rules, () =>
        {
            if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Replay context changed.");
        }, deadline.Token);
        scope.Register("Game.Operations.Shooting", args => world.Shooting((IShootable)args[0]));
        scope.Register("Game.Registry.Get", args => world.MovingObject((Guid)args[0]));
        var activation = scope.Activate();
        try
        {
            long elapsed = 0;
            for (var index = 0; index < replay.Commands.Length; index++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var item = replay.Commands[index];
                if (item == null || item.Order == null || item.Sequence != index + 1 || item.Tick != world.Snapshot().Tick ||
                    item.ElapsedTicks < elapsed || item.ElapsedTicks > (replay.Result.EndedAt - replay.Result.StartedAt).Ticks)
                    throw new InvalidDataException("Invalid command sequence or timestamp.");
                elapsed = item.ElapsedTicks;
                string? failure = null;
                try { world.Command(item.Order).Execute(); }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
                { failure = error.GetType().Name; }
                if (failure != item.Error) throw new InvalidDataException("Command outcome differs from recording.");
                if (world.Snapshot().Finished && index != replay.Commands.Length - 1)
                    throw new InvalidDataException("Commands after match termination.");
            }
            var final = world.Snapshot();
            if (!SameState(final, replay.Result.Final)) throw new InvalidDataException("Replay final state differs from result.");
            ValidateOutcome(replay.Result);
            var last = replay.Commands.LastOrDefault();
            var reason = last?.Order.Operation == "Finish" ? last.Order.Reason ?? MatchEndReason.Requested : MatchEndReason.FleetDestroyed;
            var departed = last?.Order.Operation == "Finish" ? last.Order.PlayerId : null;
            if (reason != replay.Result.Reason || departed != replay.Result.DepartedPlayerId)
                throw new InvalidDataException("Replay termination does not match result.");
            return final;
        }
        finally
        {
            try { activation.Restore(); }
            finally { scope.Close(); }
        }
    }

    private void Validate(ReplayDocument replay)
    {
        if (replay == null || replay.Version != _options.ReplayVersion || replay.Rules == null || replay.Commands == null ||
            replay.Result == null || replay.Commands.Length == 0 || replay.Commands.Length > _options.MaxCommands ||
            JsonSerializer.SerializeToUtf8Bytes(replay).Length > _options.MaxReplayBytes || replay.Digest != Digest(replay))
            throw new InvalidDataException("Invalid replay format, version, size or digest.");
        var result = replay.Result;
        if (result.Initial == null || result.Final == null || result.Match == null || result.Initial.Progress != null ||
            result.Final.Progress != null || result.Initial.Finished || !result.Final.Finished ||
            result.Initial.GameId != result.MatchId || result.Final.GameId != result.MatchId || result.Match.Id != result.MatchId ||
            result.Initial.Match != result.Match || result.Final.Match != result.Match || result.EndedAt < result.StartedAt ||
            !Enum.IsDefined(result.Reason)) throw new InvalidDataException("Invalid match metadata.");
        try
        {
            GameWorld.Validate(result.Initial);
            GameWorld.Validate(result.Final);
            replay.Rules.Validate();
        }
        catch (ArgumentException error) { throw new InvalidDataException("Invalid replay initial state or rules.", error); }
        if (result.Initial.Objects.Length > replay.Rules.MaxObjects ||
            result.Initial.Objects.Any(item => item.Fuel > replay.Rules.MaxFuel))
            throw new InvalidDataException("Replay exceeds world limits.");
    }

    internal static void ValidateOutcome(MatchResult result)
    {
        var first = result.Final.Objects.Any(item => item.Id == result.Match.First.ShipId);
        var second = result.Final.Objects.Any(item => item.Id == result.Match.Second.ShipId);
        var winner = first == second ? (Guid?)null : first ? result.Match.First.Id : result.Match.Second.Id;
        var loser = winner == null ? (Guid?)null : winner == result.Match.First.Id ? result.Match.Second.Id : result.Match.First.Id;
        if (result.WinnerId != winner || result.LoserId != loser ||
            result.FirstRating == null || result.SecondRating == null ||
            result.FirstRating.PlayerId != result.Match.First.Id || result.SecondRating.PlayerId != result.Match.Second.Id ||
            result.FirstRating.Before < 0 || result.FirstRating.After < 0 || result.SecondRating.Before < 0 || result.SecondRating.After < 0)
            throw new InvalidDataException("Invalid winner, loser or rating owner.");
    }

    internal static bool SameState(WorldSnapshot first, WorldSnapshot second) =>
        first.Version == second.Version && first.GameId == second.GameId && first.Tick == second.Tick &&
        first.Finished == second.Finished && first.ObjectSequence == second.ObjectSequence && first.Match == second.Match &&
        first.Objects.SequenceEqual(second.Objects);
}
