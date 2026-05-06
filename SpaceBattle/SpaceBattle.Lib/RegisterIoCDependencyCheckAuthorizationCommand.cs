using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCheckAuthorizationCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Check", (object[] args) =>
        {
            return new CheckAuthorizationCommand((string)args[0], (string)args[1], (string)args[2]);
        }).Execute();
    }
}
