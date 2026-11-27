using App;
using System.Text.Json;

namespace SpaceBattle.Lib;

public enum MatchKind { Duel, Training }
public enum SearchState { NotSearching, Searching, InGame }
public record MatchPlayer(Guid Id, int Rating, Guid ShipId, bool IsBot = false);
public record MatchDefinition(Guid Id, MatchKind Kind, MatchPlayer First, MatchPlayer Second)
{
    public bool IsRanked => Kind == MatchKind.Duel && !First.IsBot && !Second.IsBot;
}
public record MatchFound(MatchDefinition Match, int WorkerId);
public record SearchStatus(SearchState State, Guid? SearchId = null, MatchFound? Match = null, string? Error = null);
public record MatchOutcome(Guid MatchId, Guid? WinnerId);

public class SearchTicket
{
    private readonly TaskCompletionSource<MatchFound> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Guid Id { get; } = Guid.NewGuid();
    public Guid PlayerId { get; }
    public int Rating { get; }
    public MatchKind Kind { get; }
    internal long Started { get; }
    internal long Sequence { get; }
    internal string? Error { get; set; }

    internal SearchTicket(Guid playerId, int rating, MatchKind kind, long started, long sequence)
    {
        PlayerId = playerId;
        Rating = rating;
        Kind = kind;
        Started = started;
        Sequence = sequence;
    }

    public Task<MatchFound> WaitAsync(CancellationToken token = default) => _completion.Task.WaitAsync(token);
    internal void Complete(MatchFound match) => _completion.TrySetResult(match);
    internal void Cancel() => _completion.TrySetCanceled();
}

internal class MatchSession
{
    private readonly GameWorld _world;
    private readonly GameplayOptions _rules;
    private readonly MatchResults _results;
    private readonly MatchResultsOptions _options;
    private readonly TimeProvider _clock;
    private readonly WorldSnapshot _initial;
    private readonly DateTimeOffset _startedAt;
    private readonly long _startedTimestamp;
    private readonly long _elapsedBefore;
    private readonly List<CommandRecord> _commands;
    private readonly bool _completeHistory;
    private int _historyBytes;
    private MatchResult? _result;
    private int _committed;
    private MatchOutcome? _outcome;
    private int _stepPending;
    public MatchDefinition Definition { get; }
    public Game Game { get; set; } = null!;
    public MatchOutcome? Outcome => Volatile.Read(ref _outcome);
    public bool IsCommitted => Volatile.Read(ref _committed) != 0;
    public MatchFound Notification => new(Definition, Game.WorkerId!.Value);

    public MatchSession(WorldSnapshot initial, GameWorld world, GameplayOptions rules,
        MatchResults results, MatchResultsOptions options, TimeProvider clock)
    {
        Definition = initial.Match!;
        _world = world;
        _rules = rules;
        _results = results;
        _options = options;
        _clock = clock;
        var original = initial.Progress?.Initial ?? initial with { Progress = null };
        GameWorld.Validate(original);
        _initial = original with { Objects = original.Objects.ToArray() };
        if (_initial == null || _initial.Progress != null || _initial.Match != Definition ||
            initial.Progress is { Commands: null } || initial.Progress is { Rules: null })
            throw new ArgumentException("Invalid match checkpoint.");
        if (options.RecordHistory && JsonSerializer.SerializeToUtf8Bytes(_initial).Length * 2 + 4096 > options.MaxReplayBytes)
            throw new ArgumentException("Initial state exceeds replay capacity.");
        _startedAt = initial.Progress?.StartedAt ?? clock.GetUtcNow();
        _startedTimestamp = clock.GetTimestamp();
        _commands = initial.Progress?.Commands.ToList() ?? new();
        _elapsedBefore = Math.Max(_commands.LastOrDefault()?.ElapsedTicks ?? 0, Math.Max(0, (clock.GetUtcNow() - _startedAt).Ticks));
        _completeHistory = options.RecordHistory && (initial.Progress?.CompleteHistory ?? (initial.Tick == 0 && !initial.Finished));
        _historyBytes = _commands.Sum(command => JsonSerializer.SerializeToUtf8Bytes(command).Length);
        if (_commands.Count > options.MaxCommands || _historyBytes > options.MaxReplayBytes)
            throw new ArgumentException("Restored history exceeds limits.");
        world.BeforeOrder = BeforeOrder;
        world.AfterOrder = AfterOrder;
        if (initial.Finished) RecordOutcome(initial with { Progress = null }, initial.Progress?.Reason ?? MatchEndReason.Requested,
            initial.Progress?.DepartedPlayerId, initial.Progress?.EndedAt);
    }

    public bool ReserveStep() => Interlocked.CompareExchange(ref _stepPending, 1, 0) == 0;
    public void ReleaseStep() => Volatile.Write(ref _stepPending, 0);
    public ICommand StepCommand() => new MatchStepCommand(this);

    private void Advance()
    {
        try
        {
            var state = _world.Snapshot();
            if (CompleteIfFinished(state)) return;
            if (_options.MatchDurationTicks > 0 && state.Tick >= _options.MatchDurationTicks)
            {
                _world.Command(new WorldOrder("Finish", Reason: MatchEndReason.TimeLimit)).Execute();
                return;
            }
            foreach (var player in new[] { Definition.First, Definition.Second }.Where(player => player.IsBot))
            {
                var ship = state.Objects.SingleOrDefault(item => item.Id == player.ShipId);
                var enemy = state.Objects.FirstOrDefault(item => item.Kind == "Ship" && item.Id != player.ShipId);
                if (ship == null || enemy == null) continue;
                var angle = Math.Atan2((double)enemy.Y - ship.Y, (double)enemy.X - ship.X);
                var direction = ((int)Math.Round(angle / (Math.PI / 4)) + 8) % 8;
                if (direction != ship.Direction && ship.Fuel >= _rules.RotateFuel)
                    _world.Command(new WorldOrder("Clockwise", ship.Id)).Execute();
                else
                {
                    _world.Command(new WorldOrder("StopRotation", ship.Id)).Execute();
                    if (ship.Rockets > 0 && ship.Fuel >= _rules.ShotFuel && state.Objects.Length < _rules.MaxObjects)
                        _world.Command(new WorldOrder("Shoot", ship.Id)).Execute();
                }
            }
            _world.Command(new WorldOrder("Tick")).Execute();
            CompleteIfFinished(_world.Snapshot());
        }
        catch (Exception) when (Outcome == null)
        {
            _world.Command(new WorldOrder("Finish", Reason: MatchEndReason.TechnicalError)).Execute();
            throw;
        }
        finally { ReleaseStep(); }
    }

    private bool CompleteIfFinished(WorldSnapshot snapshot)
    {
        var first = snapshot.Objects.Any(item => item.Id == Definition.First.ShipId);
        var second = snapshot.Objects.Any(item => item.Id == Definition.Second.ShipId);
        if (!snapshot.Finished && first && second) return false;
        if (!snapshot.Finished) _world.Command(new WorldOrder("Finish", Reason: MatchEndReason.FleetDestroyed)).Execute();
        else if (Outcome == null) RecordOutcome(snapshot, MatchEndReason.FleetDestroyed, null);
        return true;
    }

    private void RecordOutcome(WorldSnapshot snapshot, MatchEndReason reason, Guid? departed, DateTimeOffset? endedAt = null)
    {
        if (Outcome != null) return;
        var first = snapshot.Objects.Any(item => item.Id == Definition.First.ShipId);
        var second = snapshot.Objects.Any(item => item.Id == Definition.Second.ShipId);
        var winner = first == second ? (Guid?)null : first ? Definition.First.Id : Definition.Second.Id;
        var loser = winner == null ? (Guid?)null : winner == Definition.First.Id ? Definition.Second.Id : Definition.First.Id;
        _result = new MatchResult(Definition.Id, Definition, _initial, snapshot, winner, loser, reason, departed,
            _startedAt, endedAt ?? _startedAt.AddTicks(Elapsed()),
            new(Definition.First.Id, Definition.First.Rating, Definition.First.Rating),
            new(Definition.Second.Id, Definition.Second.Rating, Definition.Second.Rating));
        Volatile.Write(ref _outcome, new MatchOutcome(Definition.Id, winner));
        _results.Publish(_result, _rules, _commands.ToArray(), _completeHistory,
            _ => Volatile.Write(ref _committed, 1));
    }

    private long Elapsed() => _elapsedBefore + _clock.GetElapsedTime(_startedTimestamp).Ticks;

    public WorldSnapshot Snapshot()
    {
        var state = _world.Snapshot();
        return state with
        {
            Progress = new MatchProgress(_initial with { Objects = _initial.Objects.ToArray() }, _rules, _startedAt, _commands.ToArray(),
            _completeHistory, _result?.Reason, _result?.DepartedPlayerId, _result?.EndedAt)
        };
    }

    private void BeforeOrder(WorldOrder order)
    {
        if (Outcome != null) throw new InvalidOperationException("Match is finished.");
        if (!_options.RecordHistory) return;
        var closing = false;
        if (order?.Operation == "Finish")
        {
            try
            {
                GameWorld.ValidateOrder(order, Definition);
                var state = _world.Snapshot();
                closing = order.Reason != MatchEndReason.FleetDestroyed ||
                    !state.Objects.Any(item => item.Id == Definition.First.ShipId) ||
                    !state.Objects.Any(item => item.Id == Definition.Second.ShipId);
            }
            catch (ArgumentException) { }
        }
        if (closing) return;
        var projected = _historyBytes + JsonSerializer.SerializeToUtf8Bytes(order).Length * 2 +
            JsonSerializer.SerializeToUtf8Bytes(_initial).Length + JsonSerializer.SerializeToUtf8Bytes(_world.Snapshot()).Length + 4096;
        if (_commands.Count >= _options.MaxCommands - 1 || projected > _options.MaxReplayBytes)
        {
            _world.Command(new WorldOrder("Finish", Reason: MatchEndReason.RecordingLimit)).Execute();
            throw new InvalidOperationException("Match recording limit reached.");
        }
    }

    private void AfterOrder(WorldOrder order, long tick, Exception? error)
    {
        if (Outcome != null) return;
        if (_options.RecordHistory)
        {
            var record = new CommandRecord(_commands.Count + 1, tick, Elapsed(), order, error?.GetType().Name);
            _commands.Add(record);
            _historyBytes += JsonSerializer.SerializeToUtf8Bytes(record).Length;
        }
        var snapshot = _world.Snapshot();
        if (error == null && order.Operation == "Finish")
            RecordOutcome(snapshot, order.Reason ?? MatchEndReason.Requested, order.PlayerId);
        else if (error == null) CompleteIfFinished(snapshot);
        else if (error is not (ArgumentException or InvalidOperationException or KeyNotFoundException))
            _world.Command(new WorldOrder("Finish", Reason: MatchEndReason.TechnicalError)).Execute();
    }

    private class MatchStepCommand(MatchSession session) : ICommand
    {
        public void Execute() => session.Advance();
    }
}
