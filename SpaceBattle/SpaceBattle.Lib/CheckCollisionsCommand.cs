using System;
using System.Collections.Generic;
using System.Linq;
using App;

namespace SpaceBattle.Lib;

public class CheckCollisionsCommand : ICommand
{
    private readonly ICollisionObject _target;
    private readonly IEnumerable<ICollisionObject> _objects;
    private readonly IDictionary<(string, string), CollisionTree> _storage;

    public IReadOnlyCollection<ICollisionObject> CollidedObjects { get; private set; } = [];

    public CheckCollisionsCommand(
        ICollisionObject target,
        IEnumerable<ICollisionObject> objects,
        IDictionary<(string, string), CollisionTree> storage)
    {
        _target = target;
        _objects = objects;
        _storage = storage;
    }

    public void Execute()
    {
        var collided = new List<ICollisionObject>();

        foreach (var obj in _objects)
        {
            if (ReferenceEquals(_target, obj))
                continue;

            var checkCommand = new CheckCollisionCommand(_target, obj, _storage);
            checkCommand.Execute();

            if (checkCommand.HasCollision)
                collided.Add(obj);
        }

        CollidedObjects = collided;
    }
}
