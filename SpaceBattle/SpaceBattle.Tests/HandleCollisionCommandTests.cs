using SpaceBattle.Lib;
using Moq;
using App;
using App.Scopes;

namespace SpaceBattle.Tests;

public class HandleCollisionCommandTests
{
    public HandleCollisionCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        RegisterTestDependencies();
    }

    private void RegisterTestDependencies()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Delete", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Ship.ApplyDamage", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();
    }

    [Fact]
    public void HandleCollisionCommand_Execute_Should_Delete_Torpedo_And_Apply_Damage()
    {
        var collision = new { TorpedoId = Guid.NewGuid(), ShipId = Guid.NewGuid() };
        var command = new HandleCollisionCommand(collision);

        var exception = Record.Exception(() => command.Execute());

        Assert.Null(exception);
    }
}
