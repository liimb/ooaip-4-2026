using System;
using App;

namespace SpaceBattle.Lib;

public class CheckCollisionCommand : ICommand
{
    private readonly ICollisionObject _firstObject;
    private readonly ICollisionObject _secondObject;
    private readonly IDictionary<(string, string), CollisionTree> _storage;

    public bool HasCollision { get; private set; }

    public CheckCollisionCommand(
        ICollisionObject firstObject,
        ICollisionObject secondObject,
        IDictionary<(string, string), CollisionTree> storage)
    {
        _firstObject = firstObject;
        _secondObject = secondObject;
        _storage = storage;
    }

    public void Execute()
    {
        var relativeState = GetRelativeState(_firstObject, _secondObject);

        if (_storage.TryGetValue((_firstObject.Form, _secondObject.Form), out var tree))
        {
            HasCollision = tree.Contains(relativeState);
            return;
        }

        if (_storage.TryGetValue((_secondObject.Form, _firstObject.Form), out var reverseTree))
        {
            var reverseState = GetRelativeState(_secondObject, _firstObject);
            HasCollision = reverseTree.Contains(reverseState);
            return;
        }

        HasCollision = false;
    }

    private static (int, int, int, int) GetRelativeState(ICollisionObject first, ICollisionObject second)
    {
        var firstPos = first.Position.GetCoordinates();
        var secondPos = second.Position.GetCoordinates();
        var firstVel = first.Velocity.GetCoordinates();
        var secondVel = second.Velocity.GetCoordinates();

        if (firstPos.Length < 2 || secondPos.Length < 2 ||
            firstVel.Length < 2 || secondVel.Length < 2)
        {
            throw new ArgumentException("Collision detection requires 2D vectors");
        }

        return (
            secondPos[0] - firstPos[0],
            secondPos[1] - firstPos[1],
            secondVel[0] - firstVel[0],
            secondVel[1] - firstVel[1]
        );
    }
}
