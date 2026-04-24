using App;

namespace SpaceBattle.Lib;

public class MacroCommand : ICommand
{
    private readonly ICommand[] _commands;

    public MacroCommand(params ICommand[] commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public void Execute()
    {
        _commands.ToList().ForEach(cmd => cmd?.Execute());
    }
}
