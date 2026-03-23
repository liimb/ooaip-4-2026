namespace SpaceBattle.Lib;

public class MoveCommand : ICommand
{
    private IMovingObject _moving;

    public MoveCommand(IMovingObject moving)
    {
        _moving = moving;
    }

    public void Execute()
    {
        _moving.Position += _moving.Velocity;
    }
}
