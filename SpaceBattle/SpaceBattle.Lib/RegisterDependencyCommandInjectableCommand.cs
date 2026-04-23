using App;

namespace SpaceBattle.Lib
{
    public class RegisterDependencyCommandInjectableCommand : ICommand
    {
        public void Execute()
        {
            App.Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.CommandInjectable",
                (object[] args) => new CommandInjectableCommand()
            ).Execute();
        }
    }
}
