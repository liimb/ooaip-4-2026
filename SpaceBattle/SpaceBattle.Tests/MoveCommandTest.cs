using Moq;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class MoveCommandTest
{
    [Fact]
    public void MoveShouldCorrentChangePosition()
    {
        var moving = new Mock<IMovingObject>();

        moving.SetupGet(a => a.Position).Returns(new Vector([12, 5]));
        moving.SetupGet(a => a.Velocity).Returns(new Vector([-4, 1]));

        var cmd = new MoveCommand(moving.Object);

        cmd.Execute();

        moving.VerifySet(a => a.Position = new Vector([8, 6]));
    }

    [Fact]
    public void MoveShouldCannotReadPosition()
    {
        var moving = new Mock<IMovingObject>();

        moving.SetupGet(a => a.Position).Throws<InvalidOperationException>();

        var cmd = new MoveCommand(moving.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void MoveCannotReadVelocity()
    {
        var moving = new Mock<IMovingObject>();

        moving.SetupGet(a => a.Position).Returns(new Vector([1, 1]));
        moving.SetupGet(a => a.Velocity).Throws<InvalidOperationException>();

        var cmd = new MoveCommand(moving.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }

    [Fact]
    public void MoveCannotChangePosition()
    {
        var moving = new Mock<IMovingObject>();

        moving.SetupGet(a => a.Position).Returns(new Vector([12, 5]));
        moving.SetupGet(a => a.Velocity).Returns(new Vector([-4, 1]));
        moving.SetupSet(a => a.Position = new Vector([8, 6])).Throws<InvalidOperationException>();

        var cmd = new MoveCommand(moving.Object);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute());
    }
}
