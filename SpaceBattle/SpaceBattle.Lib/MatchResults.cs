using System.Collections.Concurrent;
using System.Text.Json;

namespace SpaceBattle.Lib;

public class MatchResults
{
    private readonly SnapshotStore _store;
    private readonly MatchResultsOptions _options;
    private readonly IRatingPolicy _ratingPolicy;
    private readonly ReplayVerifier _verifier;
    private readonly ConcurrentDictionary<Guid, PendingResult> _pending = new();
    private readonly ConcurrentDictionary<Guid, int> _ratings = new();
    private readonly ConcurrentDictionary<Guid, MatchResult> _committed = new();
    private readonly SemaphoreSlim _flush = new(1, 1);
    public string? LastError { get; private set; }

    public MatchResults(MatchResultsOptions options, IRatingPolicy ratingPolicy)
    {
        options.Validate();
        _options = options;
        _ratingPolicy = ratingPolicy;
        _store = new SnapshotStore(options.StoragePath);
        _verifier = new ReplayVerifier(options);
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        var archive = await ReadAsync(token);
        foreach (var entry in archive.Entries)
        {
            UpdateRatings(entry.Result);
            _committed[entry.Result.MatchId] = entry.Result;
        }
    }

    public int Rating(Guid playerId, int fallback) => _ratings.GetValueOrDefault(playerId, fallback);
    internal bool IsCommitted(Guid matchId) => _committed.ContainsKey(matchId);
    internal MatchResult? Committed(Guid matchId) => _committed.GetValueOrDefault(matchId);

    internal void Publish(MatchResult result, GameplayOptions rules, CommandRecord[] commands, bool completeHistory, Action<MatchResult> committed)
    {
        if (_committed.TryGetValue(result.MatchId, out var existing) && ReplayVerifier.SameState(existing.Final, result.Final) &&
            existing.Match == result.Match && existing.Reason == result.Reason)
        {
            committed(existing);
            return;
        }
        _pending.TryAdd(result.MatchId, new PendingResult(result, rules, commands, completeHistory, committed));
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        await _flush.WaitAsync(token);
        try
        {
            foreach (var item in _pending.Values.OrderBy(item => item.Result.EndedAt))
            {
                var saved = await SaveAsync(item, token);
                UpdateRatings(saved);
                _committed[saved.MatchId] = saved;
                item.Committed(saved);
                _pending.TryRemove(item.Result.MatchId, out _);
            }
            LastError = null;
        }
        catch (Exception error) { LastError = error.Message; throw; }
        finally { _flush.Release(); }
    }

    private async Task<MatchResult> SaveAsync(PendingResult pending, CancellationToken token)
    {
        await using var lease = await _store.LockAsync(token);
        var archive = await ReadAsync(token);
        var existing = archive.Entries.SingleOrDefault(entry => entry.Result.MatchId == pending.Result.MatchId);
        if (existing != null)
        {
            if (!ReplayVerifier.SameState(existing.Result.Final, pending.Result.Final) || existing.Result.Match != pending.Result.Match ||
                existing.Result.Reason != pending.Result.Reason)
                throw new InvalidDataException("A different result already exists for this match.");
            return existing.Result;
        }
        var result = pending.Result;
        var first = CurrentRating(archive, result.Match.First.Id, result.Match.First.Rating);
        var second = CurrentRating(archive, result.Match.Second.Id, result.Match.Second.Rating);
        var ratings = result.Match.IsRanked && result.Reason is MatchEndReason.FleetDestroyed or MatchEndReason.TimeLimit
            ? _ratingPolicy.Calculate(result, first, second) : (first, second);
        result = result with
        {
            FirstRating = new(result.Match.First.Id, first, ratings.Item1),
            SecondRating = new(result.Match.Second.Id, second, ratings.Item2)
        };
        ReplayDocument? replay = null;
        if (_options.RecordReplays && pending.CompleteHistory)
        {
            replay = new ReplayDocument(_options.ReplayVersion, pending.Rules, pending.Commands, result, string.Empty);
            replay = replay with { Digest = ReplayVerifier.Digest(replay) };
            await _verifier.PlayAsync(replay, token);
        }
        ReplayVerifier.ValidateOutcome(result);
        var entry = new MatchArchiveEntry(result, _options.RecordHistory ? pending.Commands : null, replay);
        await _store.WriteDocumentAsync(new MatchArchive(1, archive.Entries.Append(entry).ToArray()), token);
        return result;
    }

    private static int CurrentRating(MatchArchive archive, Guid player, int fallback)
    {
        foreach (var entry in archive.Entries.Reverse())
        {
            if (entry.Result.Match.First.Id == player) return entry.Result.FirstRating.After;
            if (entry.Result.Match.Second.Id == player) return entry.Result.SecondRating.After;
        }
        return fallback;
    }

    private void UpdateRatings(MatchResult result)
    {
        if (!result.Match.First.IsBot) _ratings[result.Match.First.Id] = result.FirstRating.After;
        if (!result.Match.Second.IsBot) _ratings[result.Match.Second.Id] = result.SecondRating.After;
    }

    private async Task<MatchArchive> ReadAsync(CancellationToken token)
    {
        var archive = await _store.ReadDocumentAsync<MatchArchive>(token) ?? new MatchArchive(1, Array.Empty<MatchArchiveEntry>());
        if (archive.Version != 1 || archive.Entries == null || archive.Entries.Any(entry => entry?.Result == null) ||
            archive.Entries.Select(entry => entry.Result.MatchId).Distinct().Count() != archive.Entries.Length)
            throw new InvalidDataException("Invalid match archive.");
        return archive;
    }

    public async Task<MatchResult?> GetAsync(Guid matchId, CancellationToken token = default) =>
        (await ReadAsync(token)).Entries.SingleOrDefault(entry => entry.Result.MatchId == matchId)?.Result;

    public async Task<MatchResult[]> HistoryAsync(Guid playerId, CancellationToken token = default) =>
        (await ReadAsync(token)).Entries.Select(entry => entry.Result)
            .Where(result => result.Match.First.Id == playerId || result.Match.Second.Id == playerId).OrderByDescending(result => result.EndedAt).ToArray();

    public async Task<CommandRecord[]?> CommandsAsync(Guid matchId, CancellationToken token = default) =>
        (await ReadAsync(token)).Entries.SingleOrDefault(entry => entry.Result.MatchId == matchId)?.History;

    public async Task<ReplayDocument?> LoadReplayAsync(Guid matchId, CancellationToken token = default) =>
        (await ReadAsync(token)).Entries.SingleOrDefault(entry => entry.Result.MatchId == matchId)?.Replay;

    public async Task<ReplayMetadata[]> ListReplaysAsync(CancellationToken token = default) =>
        (await ReadAsync(token)).Entries.Where(entry => entry.Replay != null).Select(entry => new ReplayMetadata(
            entry.Result.MatchId, entry.Result.Match.Kind, entry.Result.StartedAt, entry.Result.EndedAt,
            entry.Replay!.Commands.Length, entry.Replay.Version)).ToArray();

    public async Task<WorldSnapshot> ReplayAsync(Guid matchId, CancellationToken token = default)
    {
        var replay = await LoadReplayAsync(matchId, token) ?? throw new KeyNotFoundException("Replay does not exist.");
        return await _verifier.PlayAsync(replay, token);
    }

    public async Task<bool> DeleteReplayAsync(Guid matchId, CancellationToken token = default)
    {
        await using var lease = await _store.LockAsync(token);
        var archive = await ReadAsync(token);
        var index = Array.FindIndex(archive.Entries, entry => entry.Result.MatchId == matchId);
        if (index < 0 || archive.Entries[index].Replay == null) return false;
        archive.Entries[index] = archive.Entries[index] with { Replay = null };
        await _store.WriteDocumentAsync(archive, token);
        return true;
    }

    private record PendingResult(MatchResult Result, GameplayOptions Rules, CommandRecord[] Commands,
        bool CompleteHistory, Action<MatchResult> Committed);
}
