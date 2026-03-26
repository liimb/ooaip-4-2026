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
        ExecuteCommands(_commands);
    }

    private void ExecuteCommands(ICommand[] commands)
    {
        if (commands == null || commands.Length == 0) return;

        var first = commands[0];
        var rest = commands.Skip(1).ToArray();

        if (first != null)
        {
            first.Execute();
        }

        ExecuteCommands(rest);
    }
}
