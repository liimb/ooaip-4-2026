using System;
using System.Collections.Generic;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class MoveWithCollisionCheckCommandTest
{
    public MoveWithCollisionCheckCommandTest()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        try
        {
            Ioc.Resolve<App.ICommand>(
                "IoC.Register",
                "Commands.Move",
                (object[] args) =>
                {
                    var movable = (IMovingObject)args[0];
                    return new MoveCommand(movable);
                }
            ).Execute();
        }
        catch (ArgumentException)
        {
        }
    }

    [Fact]
    public void Execute_MovesObjectAndChecksCollisionsWithObjectsCollection()
    {
        var storage = new Dictionary<(string, string), CollisionTree>
        {
            [("ship", "torpedo")] = new CollisionTree([(1, 0, 0, 0)])
        };
        var shipDict = CreateGameObject("ship", [0, 0], [1, 0]);
        var torpedoDict = CreateGameObject("torpedo", [2, 0], [1, 0]);

        // Создаём адаптеры напрямую
        var shipMoving = new MovingDictionaryAdapter(shipDict);
        var shipCollision = new CollisionDictionaryAdapter(shipDict);
        var torpedoCollision = new CollisionDictionaryAdapter(torpedoDict);

        var allObjects = new ICollisionObject[] { shipCollision, torpedoCollision };

        // Создаём команды напрямую
        var moveCommand = new MoveCommand(shipMoving);
        var checkCommand = new CheckCollisionsCommand(shipCollision, allObjects, storage);
        var command = new MoveWithCollisionCheckCommand(moveCommand, checkCommand);

        command.Execute();

        Assert.Equal(new Vector([1, 0]), shipMoving.Position);
        Assert.Single(command.CollidedObjects);
        Assert.Equal("torpedo", command.CollidedObjects.Single().Form);
    }

    private static Dictionary<string, object> CreateGameObject(string form, int[] position, int[] velocity)
    {
        return new Dictionary<string, object>
        {
            ["Form"] = form,
            ["Position"] = new Vector(position),
            ["Velocity"] = new Vector(velocity)
        };
    }

    private sealed class MovingDictionaryAdapter : IMovingObject
    {
        private readonly IDictionary<string, object> _gameObject;

        public MovingDictionaryAdapter(IDictionary<string, object> gameObject)
        {
            _gameObject = gameObject;
        }

        public Vector Position
        {
            get => (Vector)_gameObject["Position"];
            set => _gameObject["Position"] = value;
        }

        public Vector Velocity => (Vector)_gameObject["Velocity"];
    }

    private sealed class CollisionDictionaryAdapter : ICollisionObject
    {
        private readonly IDictionary<string, object> _gameObject;

        public CollisionDictionaryAdapter(IDictionary<string, object> gameObject)
        {
            _gameObject = gameObject;
        }

        public string Form => (string)_gameObject["Form"];

        public IVector Position => new VectorAdapter((Vector)_gameObject["Position"]);

        public IVector Velocity => new VectorAdapter((Vector)_gameObject["Velocity"]);
    }

    private sealed class VectorAdapter : IVector
    {
        private readonly Vector _vector;

        public VectorAdapter(Vector vector)
        {
            _vector = vector;
        }

        public int[] GetCoordinates()
        {
            var field = typeof(Vector).GetField("coordinates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (int[])field!.GetValue(_vector)!;
        }
    }
}
