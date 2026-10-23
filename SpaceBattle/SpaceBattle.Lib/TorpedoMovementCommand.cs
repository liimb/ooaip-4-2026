using App;

namespace SpaceBattle.Lib;

public class TorpedoMovementCommand : ICommand
{
    private readonly Guid _torpedoId;

    public TorpedoMovementCommand(Guid torpedoId)
    {
        _torpedoId = torpedoId;
    }

    public void Execute()
    {
        var torpedo = Ioc.Resolve<IMovingObject>("Game.Registry.Get", _torpedoId);
        var moveCommand = new MoveCommand(torpedo);
        moveCommand.Execute();
    }
}
