using System;
using App;
namespace SpaceBattle.Lib;

public class MoveWithCollisionCheckCommand : ICommand
{
    private readonly ICommand _moveCommand;
    private readonly CheckCollisionsCommand _checkCollisionsCommand;

    public IReadOnlyCollection<ICollisionObject> CollidedObjects => _checkCollisionsCommand.CollidedObjects;

    public MoveWithCollisionCheckCommand(ICommand moveCommand, CheckCollisionsCommand checkCollisionsCommand)
    {
        _moveCommand = moveCommand;
        _checkCollisionsCommand = checkCollisionsCommand;
    }

    public void Execute()
    {
        _moveCommand.Execute();
        _checkCollisionsCommand.Execute();
    }
}
