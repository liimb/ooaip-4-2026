using Moq;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Lib.Tests;

public class TorpedoMovementTests
{
    public TorpedoMovementTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void TorpedoMovementCommand_MovesTorpedo()
    {
        var torpedoId = Guid.NewGuid();
        var startPosition = new Vector([10, 20]);
        var velocity = new Vector([3, 4]);

        var mockTorpedo = new Mock<IMovingObject>();
        mockTorpedo.SetupProperty(m => m.Position, startPosition);
        mockTorpedo.SetupGet(m => m.Velocity).Returns(velocity);

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Get", (object[] args) =>
        {
            return mockTorpedo.Object;
        }).Execute();

        var command = new TorpedoMovementCommand(torpedoId);
        command.Execute();

        Assert.Equal(new Vector([13, 24]), mockTorpedo.Object.Position);
    }
}
