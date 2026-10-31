using App;
using System;
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

                if (args[0] is string[] commandNames)
                {
                    var resolvedCommands = commandNames
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => Ioc.Resolve<ICommand>($"Commands.{name}"))
                        .ToArray();

                    return new MacroCommand(resolvedCommands);
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
