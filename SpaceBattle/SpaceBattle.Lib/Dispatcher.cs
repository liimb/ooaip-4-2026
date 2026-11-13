using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib;

public class Dispatcher
{
    private readonly ConcurrentDictionary<Guid, Route> _routes = new();
    private readonly object _admission = new();
    private int _accepting = 1;

    public DispatchResult Dispatch(Guid gameId, ICommand command)
    {
        if (gameId == Guid.Empty || command == null) return DispatchResult.InvalidMessage;
        if (!Monitor.TryEnter(_admission)) return DispatchResult.Overloaded;
        try
        {
            if (_accepting == 0) return DispatchResult.Stopped;
            if (!_routes.TryGetValue(gameId, out var route)) return DispatchResult.UnknownGame;
            if (route.Game.State == GameState.Completed) return DispatchResult.Stopped;
            if (!route.Game.Reserve()) return DispatchResult.Overloaded;
            return route.Scheduler.TrySend(route.Game, command) ? DispatchResult.Accepted : DispatchResult.Overloaded;
        }
        finally { Monitor.Exit(_admission); }
    }

    internal void Register(Game game, GameScheduler scheduler)
    {
        if (!_routes.TryAdd(game.Id, new Route(game, scheduler)))
            throw new InvalidOperationException("Game ID is already registered.");
    }

    internal void StopAccepting()
    {
        lock (_admission) _accepting = 0;
    }

    private record Route(Game Game, GameScheduler Scheduler);
}
