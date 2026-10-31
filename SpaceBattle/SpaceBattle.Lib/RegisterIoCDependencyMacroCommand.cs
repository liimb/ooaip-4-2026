using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyMacroCommand : ICommand
{
    public void Execute()
    {
        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Macro",
                (object[] args) =>
                {
                    return Ioc.Resolve<ICommand>("Commands.Factory.Generated", args);
                }
            ).Execute();
        }
        catch (System.ArgumentException) { }
    }
}
