using SpaceBattle.Lib;
using Moq;
using App;
using App.Scopes;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyMoveCommandTest
{
    public RegisterIoCDependencyMoveCommandTest()
    {
        new InitCommand().Execute();
    }

    [Fact]
    public void ShouldResolveMoveCommand()
    {
        var moving = new Mock<IMovingObject>();
        moving.SetupProperty(m => m.Position, new Vector([0, 0]));
        moving.SetupGet(m => m.Velocity).Returns(new Vector([1, 1]));

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Adapters.IMovingObject",
            (object[] args) => moving.Object
        ).Execute();

        new RegisterIoCDependencyMoveCommand().Execute();

        var cmd = Ioc.Resolve<ICommand>("Commands.Move", moving.Object);

        cmd.Execute();

        Assert.Equal(new Vector([1, 1]), moving.Object.Position);
    }
}
