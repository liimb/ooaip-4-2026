using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyMacroMoveRotate : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Macro.Move",
            (object[] args) =>
            {
                var strategy = new CreateMacroCommandStrategy("Move");
                return strategy.Resolve(args);
            }
        ).Execute();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Macro.Rotate",
            (object[] args) =>
            {
                var strategy = new CreateMacroCommandStrategy("Rotate");
                return strategy.Resolve(args);
            }
        ).Execute();
    }
}
