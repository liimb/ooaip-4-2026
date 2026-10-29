using System.Collections.Generic;
using Moq;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CheckCollisionCommandTests
{
    [Fact]
    public void Execute_FindsCollisionInStorageDirectly()
    {
        var pos1 = new Mock<IVector>();
        pos1.Setup(p => p.GetCoordinates()).Returns(new[] { 0, 0 });
        var vel1 = new Mock<IVector>();
        vel1.Setup(v => v.GetCoordinates()).Returns(new[] { 1, 1 });

        var obj1 = new Mock<ICollisionObject>();
        obj1.SetupGet(o => o.Form).Returns("circle");
        obj1.SetupGet(o => o.Position).Returns(pos1.Object);
        obj1.SetupGet(o => o.Velocity).Returns(vel1.Object);

        var pos2 = new Mock<IVector>();
        pos2.Setup(p => p.GetCoordinates()).Returns(new[] { 2, 2 });
        var vel2 = new Mock<IVector>();
        vel2.Setup(v => v.GetCoordinates()).Returns(new[] { 3, 3 });

        var obj2 = new Mock<ICollisionObject>();
        obj2.SetupGet(o => o.Form).Returns("circle");
        obj2.SetupGet(o => o.Position).Returns(pos2.Object);
        obj2.SetupGet(o => o.Velocity).Returns(vel2.Object);

        var tree = new CollisionTree(new List<(int, int, int, int)> { (2, 2, 2, 2) });

        var storage = new Dictionary<(string, string), CollisionTree>
        {
            { ("circle", "circle"), tree }
        };

        var cmd = new CheckCollisionCommand(obj1.Object, obj2.Object, storage);

        cmd.Execute();

        Assert.True(cmd.HasCollision);
    }

    [Fact]
    public void Execute_FindsCollisionInStorageReverse()
    {
        var pos1 = new Mock<IVector>();
        pos1.Setup(p => p.GetCoordinates()).Returns(new[] { 0, 0 });
        var vel1 = new Mock<IVector>();
        vel1.Setup(v => v.GetCoordinates()).Returns(new[] { 1, 1 });

        var obj1 = new Mock<ICollisionObject>();
        obj1.SetupGet(o => o.Form).Returns("circle");
        obj1.SetupGet(o => o.Position).Returns(pos1.Object);
        obj1.SetupGet(o => o.Velocity).Returns(vel1.Object);

        var pos2 = new Mock<IVector>();
        pos2.Setup(p => p.GetCoordinates()).Returns(new[] { 2, 2 });
        var vel2 = new Mock<IVector>();
        vel2.Setup(v => v.GetCoordinates()).Returns(new[] { 3, 3 });

        var obj2 = new Mock<ICollisionObject>();
        obj2.SetupGet(o => o.Form).Returns("rectangle");
        obj2.SetupGet(o => o.Position).Returns(pos2.Object);
        obj2.SetupGet(o => o.Velocity).Returns(vel2.Object);

        var tree = new CollisionTree(new List<(int, int, int, int)> { (-2, -2, -2, -2) });

        var storage = new Dictionary<(string, string), CollisionTree>
        {
            { ("rectangle", "circle"), tree }
        };

        var cmd = new CheckCollisionCommand(obj1.Object, obj2.Object, storage);

        cmd.Execute();

        Assert.True(cmd.HasCollision);
    }

    [Fact]
    public void Execute_NoCollisionFound_SetsFalse()
    {
        var pos1 = new Mock<IVector>();
        pos1.Setup(p => p.GetCoordinates()).Returns(new[] { 0, 0 });
        var vel1 = new Mock<IVector>();
        vel1.Setup(v => v.GetCoordinates()).Returns(new[] { 1, 1 });

        var obj1 = new Mock<ICollisionObject>();
        obj1.SetupGet(o => o.Form).Returns("circle");
        obj1.SetupGet(o => o.Position).Returns(pos1.Object);
        obj1.SetupGet(o => o.Velocity).Returns(vel1.Object);

        var pos2 = new Mock<IVector>();
        pos2.Setup(p => p.GetCoordinates()).Returns(new[] { 2, 2 });
        var vel2 = new Mock<IVector>();
        vel2.Setup(v => v.GetCoordinates()).Returns(new[] { 3, 3 });

        var obj2 = new Mock<ICollisionObject>();
        obj2.SetupGet(o => o.Form).Returns("triangle");
        obj2.SetupGet(o => o.Position).Returns(pos2.Object);
        obj2.SetupGet(o => o.Velocity).Returns(vel2.Object);

        var storage = new Dictionary<(string, string), CollisionTree>();

        var cmd = new CheckCollisionCommand(obj1.Object, obj2.Object, storage);

        cmd.Execute();

        Assert.False(cmd.HasCollision);
    }

    [Fact]
    public void Execute_ThrowsArgumentException_WhenVectorLengthIsLessThanTwo()
    {
        var pos1 = new Mock<IVector>();
        pos1.Setup(p => p.GetCoordinates()).Returns(new[] { 0 });
        var vel1 = new Mock<IVector>();
        vel1.Setup(v => v.GetCoordinates()).Returns(new[] { 1, 1 });

        var obj1 = new Mock<ICollisionObject>();
        obj1.SetupGet(o => o.Form).Returns("circle");
        obj1.SetupGet(o => o.Position).Returns(pos1.Object);
        obj1.SetupGet(o => o.Velocity).Returns(vel1.Object);

        var pos2 = new Mock<IVector>();
        pos2.Setup(p => p.GetCoordinates()).Returns(new[] { 2, 2 });
        var vel2 = new Mock<IVector>();
        vel2.Setup(v => v.GetCoordinates()).Returns(new[] { 3, 3 });

        var obj2 = new Mock<ICollisionObject>();
        obj2.SetupGet(o => o.Form).Returns("circle");
        obj2.SetupGet(o => o.Position).Returns(pos2.Object);
        obj2.SetupGet(o => o.Velocity).Returns(vel2.Object);

        var storage = new Dictionary<(string, string), CollisionTree>();
        var cmd = new CheckCollisionCommand(obj1.Object, obj2.Object, storage);

        Assert.Throws<System.ArgumentException>(() => cmd.Execute());
    }
}
