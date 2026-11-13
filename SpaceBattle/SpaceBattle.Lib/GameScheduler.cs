using App;

namespace SpaceBattle.Lib;

public class GameScheduler
{
    private readonly Worker _worker;
    private readonly Queue<Game> _ready = new();
    private int _pending;
    private int _gameCount;

    internal GameScheduler(Worker worker)
    {
        _worker = worker;
        worker.RunScheduled = RunNext;
    }

    public int PendingCount => Volatile.Read(ref _pending);
    public int GameCount => Volatile.Read(ref _gameCount);

    internal void Assign(Game game)
    {
        game.Bind(_worker, () => Interlocked.Decrement(ref _pending));
        Interlocked.Increment(ref _gameCount);
    }

    internal bool TrySend(Game game, ICommand command)
    {
        Interlocked.Increment(ref _pending);
        if (_worker.TrySend(new Delivery(this, game, command))) return true;
        game.Release();
        return false;
    }

    private void Deliver(Game game, ICommand command)
    {
        game.Enqueue(command);
        if (!game.Scheduled && game.HasCommands)
        {
            game.Scheduled = true;
            _ready.Enqueue(game);
        }
    }

    private bool RunNext()
    {
        if (!_ready.TryDequeue(out var game)) return false;
        try { game.Execute(); }
        catch (Exception error) { game.Abort(error); }
        if (game.HasCommands) _ready.Enqueue(game);
        else game.Scheduled = false;
        return true;
    }

    private class Delivery(GameScheduler scheduler, Game game, ICommand command) : ICommand
    {
        public void Execute() => scheduler.Deliver(game, command);
    }
}
