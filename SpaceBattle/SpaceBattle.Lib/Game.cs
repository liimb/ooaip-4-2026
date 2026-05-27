using App;

namespace SpaceBattle.Lib;

public class Game : ICommand
{
    private readonly Guid _gameId;
    private readonly object _scope;

    public Game(Guid gameId, object scope)
    {
        _gameId = gameId;
        _scope = scope;
    }

    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _scope).Execute();

        while (Ioc.Resolve<bool>("Game.Scheduler.CanContinue"))
        {
            var cmd = Ioc.Resolve<ICommand>("Game.Scheduler.Take");

            try
            {
                cmd.Execute();
            }
            catch (Exception e)
            {
                var handler = Ioc.Resolve<ICommand>("ExceptionHandler.Handle", cmd, e);
                handler.Execute();
            }
        }
    }
}
