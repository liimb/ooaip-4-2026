using Moq;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Lib.Tests;

public class ShootCommandTests
{
    public ShootCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void ShootCommand_Execute_CallsGameOperationsShooting()
    {
        var mockShootable = new Mock<IShootable>();
        var mockCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Operations.Shooting", (object[] args) =>
        {
            Assert.Equal(mockShootable.Object, args[0]);
            return mockCommand.Object;
        }).Execute();

        var shootCommand = new ShootCommand(mockShootable.Object);
        shootCommand.Execute();

        mockCommand.Verify(c => c.Execute(), Times.Once);
    }
}
