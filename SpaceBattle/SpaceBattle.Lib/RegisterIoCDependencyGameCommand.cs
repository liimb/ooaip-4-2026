using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Create", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            var scope = Ioc.Resolve<object>("Scopes.Create");
            return new Game(gameId, scope);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Scheduler.CanContinue", (object[] args) =>
        {
            return (object)false;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Scheduler.Take", (Func<object[], object>)((object[] args) =>
        {
            return null!;
        })).Execute();
    }
}
