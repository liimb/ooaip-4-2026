using App;
using System.Linq;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyMacroCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Macro",
            (object[] args) =>
            {
                if (args == null || args.Length == 0)
                {
                    return new MacroCommand();
                }

                if (args[0] is ICommand[] array)
                {
                    return new MacroCommand(array);
                }

                var commands = args.Cast<ICommand>().ToArray();
                return new MacroCommand(commands);
            }
        ).Execute();
    }
}
