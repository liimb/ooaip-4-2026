using App;

namespace SpaceBattle.Lib;

public class ScopedCommand : ICommand
{
    private readonly GameScope _scope;
    private readonly ICommand _command;
    public ScopedCommand(GameScope scope, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(command);
        _scope = scope;
        _command = command;
    }

    public void Execute()
    {
        var activation = _scope.Activate();
        try
        {
            _command.Execute();
        }
        finally
        {
            activation.Restore();
        }
    }
}
