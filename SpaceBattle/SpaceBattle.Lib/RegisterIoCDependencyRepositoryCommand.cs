using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyRepositoryCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Get", (object[] args) =>
        {
            return Ioc.Resolve<object>("Game.Objects.Find", args[0]);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Create", (object[] args) =>
        {
            return Ioc.Resolve<ICommand>("Game.Objects.Store", args[0], args[1]);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Delete", (object[] args) =>
        {
            return Ioc.Resolve<ICommand>("Game.Objects.Remove", args[0]);
        }).Execute();
    }
}
