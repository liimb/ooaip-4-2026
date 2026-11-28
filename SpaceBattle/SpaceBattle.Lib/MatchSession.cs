using App;

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
    private MatchOutcome? _outcome;
    private int _stepPending;
    public MatchDefinition Definition { get; }
    public Game Game { get; set; } = null!;
    public MatchOutcome? Outcome => Volatile.Read(ref _outcome);
    public MatchFound Notification => new(Definition, Game.WorkerId!.Value);

    public MatchSession(WorldSnapshot initial, GameWorld world, GameplayOptions rules)
    {
        Definition = initial.Match!;
        _world = world;
        _rules = rules;
        if (initial.Finished) RecordOutcome(initial);
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
        finally { ReleaseStep(); }
    }

    private bool CompleteIfFinished(WorldSnapshot snapshot)
    {
        var first = snapshot.Objects.Any(item => item.Id == Definition.First.ShipId);
        var second = snapshot.Objects.Any(item => item.Id == Definition.Second.ShipId);
        if (!snapshot.Finished && first && second) return false;
        if (!snapshot.Finished) _world.Command(new WorldOrder("Finish")).Execute();
        RecordOutcome(snapshot);
        return true;
    }

    private void RecordOutcome(WorldSnapshot snapshot)
    {
        var first = snapshot.Objects.Any(item => item.Id == Definition.First.ShipId);
        var second = snapshot.Objects.Any(item => item.Id == Definition.Second.ShipId);
        var winner = first == second ? (Guid?)null : first ? Definition.First.Id : Definition.Second.Id;
        Interlocked.CompareExchange(ref _outcome, new MatchOutcome(Definition.Id, winner), null);
    }

    private class MatchStepCommand(MatchSession session) : ICommand
    {
        public void Execute() => session.Advance();
    }
}
