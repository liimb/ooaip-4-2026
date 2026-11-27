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
            if (route.Paused) return DispatchResult.Overloaded;
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

    internal DispatchResult Service(Guid id, ICommand command)
    {
        lock (_admission)
        {
            if (!_routes.TryGetValue(id, out var route)) return DispatchResult.UnknownGame;
            if (route.Game.State == GameState.Completed) return DispatchResult.Stopped;
            if (route.Paused) return DispatchResult.Overloaded;
            if (!route.Game.Reserve()) return DispatchResult.Overloaded;
            return route.Scheduler.TrySend(route.Game, command) ? DispatchResult.Accepted : DispatchResult.Overloaded;
        }
    }

    internal Task Transfer(Guid id, GameScheduler target)
    {
        lock (_admission)
        {
            if (_accepting == 0) throw new InvalidOperationException("Server is stopping.");
            if (!_routes.TryGetValue(id, out var route)) throw new KeyNotFoundException("Unknown game.");
            if (route.Game.State == GameState.Completed) throw new InvalidOperationException("Game is completed.");
            if (route.Paused) throw new InvalidOperationException("Game is already moving.");
            if (ReferenceEquals(target, route.Scheduler)) return Task.CompletedTask;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            route.TransferCompletion = completion;
            route.Paused = true;
            route.Game.ReserveControl();
            var source = route.Scheduler;
            var marker = new TransferMarker(() => route.Game.AfterQuantum = () =>
            {
                if (!target.Post(() =>
                {
                    lock (_admission)
                    {
                        try
                        {
                            target.TransferIn(route.Game);
                            source.TransferOut();
                            route.Scheduler = target;
                            completion.TrySetResult();
                        }
                        catch (Exception error) { completion.TrySetException(error); }
                        finally { route.Paused = false; }
                    }
                }))
                {
                    lock (_admission) route.Paused = false;
                    completion.TrySetException(new InvalidOperationException("Destination worker queue is full or stopped."));
                }
            });
            if (!source.TrySend(route.Game, marker))
            {
                route.Paused = false;
                completion.TrySetException(new InvalidOperationException("Source worker queue is full or stopped."));
            }
            return completion.Task;
        }
    }

    private class Route(Game game, GameScheduler scheduler)
    {
        public Game Game { get; } = game;
        public GameScheduler Scheduler { get; set; } = scheduler;
        public bool Paused { get; set; }
        public TaskCompletionSource? TransferCompletion { get; set; }
    }
    internal void CancelTransfers()
    {
        lock (_admission)
            foreach (var route in _routes.Values) route.TransferCompletion?.TrySetCanceled();
    }
    private class TransferMarker(Action action) : ICommand
    {
        public void Execute() => action();
    }
}
