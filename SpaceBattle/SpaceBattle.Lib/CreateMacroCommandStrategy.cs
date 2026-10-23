using App;
using System.Linq;

namespace SpaceBattle.Lib;

public class CreateMacroCommandStrategy
{
    private readonly string _commandSpec;

    public CreateMacroCommandStrategy(string commandSpec)
    {
        _commandSpec = commandSpec ?? throw new ArgumentNullException(nameof(commandSpec));
    }

    public ICommand Resolve(object[] args)
    {
        var commandNames = Ioc.Resolve<string[]>("Specs." + _commandSpec);

        if (commandNames == null || commandNames.Length == 0)
            throw new InvalidOperationException($"No commands found for spec: {_commandSpec}");

        var commands = commandNames
            .Select(name => Ioc.Resolve<ICommand>("Commands." + name, args))
            .ToArray();

        return new MacroCommand(commands);
    }
}
