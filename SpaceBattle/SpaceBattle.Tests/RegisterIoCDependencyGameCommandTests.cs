using SpaceBattle.Lib;
using Moq;
using App;
using App.Scopes;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyGameCommandTests
{
    public RegisterIoCDependencyGameCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void Execute_ShouldRegisterAllDependenciesWithoutErrors()
    {
        var registerCommand = new RegisterIoCDependencyGameCommand();

        var exception = Record.Exception(() => registerCommand.Execute());

        Assert.Null(exception);
    }

    [Fact]
    public void AfterExecute_ShouldResolveGameCreate()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.CommandReceiver", (object[] args) => commandReceiverMock.Object).Execute();

        new RegisterIoCDependencyGameCommand().Execute();

        var game = Ioc.Resolve<Game>("Game.Create", gameId);

        Assert.NotNull(game);
    }

    [Fact]
    public void AfterExecute_ShouldResolveCommandsShoot()
    {
        var shootableMock = new Mock<IShootable>();
        shootableMock.Setup(s => s.Position).Returns(new Vector([0, 0]));
        shootableMock.Setup(s => s.Angle).Returns(new Angle(0));

        new RegisterIoCDependencyGameCommand().Execute();

        var cmd = Ioc.Resolve<ICommand>("Commands.Shoot", shootableMock.Object);

        Assert.NotNull(cmd);
        Assert.IsType<ShootCommand>(cmd);
    }

    [Fact]
    public void AfterExecute_ShouldResolveCommandsTorpedoMove()
    {
        var torpedoId = Guid.NewGuid();

        new RegisterIoCDependencyGameCommand().Execute();

        var cmd = Ioc.Resolve<ICommand>("Commands.TorpedoMove", torpedoId);

        Assert.NotNull(cmd);
        Assert.IsType<TorpedoMovementCommand>(cmd);
    }
}
