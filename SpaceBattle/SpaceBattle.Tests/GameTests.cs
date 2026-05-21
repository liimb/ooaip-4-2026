using SpaceBattle.Lib;
using App;
using App.Scopes;
using Moq;

namespace SpaceBattle.Tests;

public class GameTests
{
    public GameTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        RegisterTestDependencies();
    }

    private void RegisterTestDependencies()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Operations.Shooting", (object[] args) =>
        {
            var shootable = (IShootable)args[0];
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Get", (object[] args) =>
        {
            var id = (Guid)args[0];
            var movingObjectMock = new Mock<IMovingObject>();
            movingObjectMock.Setup(m => m.Position).Returns(new Vector(new int[] { 0, 0 }));
            movingObjectMock.Setup(m => m.Velocity).Returns(new Vector(new int[] { 0, 0 }));
            return movingObjectMock.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Create", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Registry.Delete", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Find", (object[] args) =>
        {
            return new object();
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Store", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            return mockCommand.Object;
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Remove", (object[] args) =>
        {
            var mockCommand = new Mock<ICommand>();
            return mockCommand.Object;
        }).Execute();
    }

    [Fact]
    public void Game_Should_Send_Command_To_Receiver_When_AddCommand_Called()
    {
        var gameId = Guid.NewGuid();
        var commandMock = new Mock<ICommand>();
        var cmd = commandMock.Object;

        var commandReceiverMock = new Mock<ICommandReceiver>();
        var receiver = commandReceiverMock.Object;

        var game = new Game(gameId, receiver);

        game.AddCommand(cmd);

        commandReceiverMock.Verify(r => r.Receive(It.IsAny<ICommand>()), Times.Once());
    }

    [Fact]
    public void Game_Shoot_Should_Create_ShootCommand_And_Check_Auth()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var shootableMock = new Mock<IShootable>();
        var position = new Vector(new int[] { 0, 0 });
        var angle = new Angle(0);

        shootableMock.Setup(s => s.Position).Returns(position);
        shootableMock.Setup(s => s.Angle).Returns(angle);
        shootableMock.Setup(s => s.ToString()).Returns("TestShip");

        var authTree = new PrefixTree<string, bool>();
        authTree.Add(["Player", "TestShip", "Shoot"], true);
        Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Tree", (object[] args) => authTree).Execute();

        var exception = Record.Exception(() => game.Shoot(shootableMock.Object));
        Assert.Null(exception);
    }

    [Fact]
    public void Game_Update_Should_Move_Torpedoes()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var torpedoId = Guid.NewGuid();
        var torpedoesList = new List<Guid> { torpedoId };

        Ioc.Resolve<ICommand>("IoC.Register", "Game.GetTorpedoes", (object[] args) => torpedoesList).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.CheckCollisions", (object[] args) => new List<object>()).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.HandleCollision", (object[] args) => new Mock<ICommand>().Object).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "MoveCommand", (object[] args) =>
        {
            var movingObject = (IMovingObject)args[0];
            var mockCommand = new Mock<ICommand>();
            mockCommand.Setup(c => c.Execute()).Verifiable();
            return mockCommand.Object;
        }).Execute();

        var exception = Record.Exception(() => game.Update());

        Assert.Null(exception);
    }

    [Fact]
    public void Game_Should_Stop_When_Stop_Called()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        game.Stop();

        Assert.False(game.IsRunning);
    }

    [Fact]
    public void Game_Shoot_Should_Throw_When_Not_Authorized()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var shootableMock = new Mock<IShootable>();
        var position = new Vector(new int[] { 0, 0 });
        var angle = new Angle(0);

        shootableMock.Setup(s => s.Position).Returns(position);
        shootableMock.Setup(s => s.Angle).Returns(angle);
        shootableMock.Setup(s => s.ToString()).Returns("TestShip");

        var authTree = new PrefixTree<string, bool>();
        Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Tree", (object[] args) => authTree).Execute();

        Assert.Throws<UnauthorizedAccessException>(() => game.Shoot(shootableMock.Object));
    }

    [Fact]
    public void Game_Shoot_Should_Throw_When_Shooter_Is_Null()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        Assert.Throws<ArgumentNullException>(() => game.Shoot(null!));
    }

    [Fact]
    public void Game_Update_Should_Handle_Collisions_When_Collisions_Exist()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var torpedoId = Guid.NewGuid();
        var torpedoesList = new List<Guid> { torpedoId };
        var collision = new { TorpedoId = torpedoId, ShipId = Guid.NewGuid() };
        var collisionsList = new List<object> { collision };

        var handleCollisionMock = new Mock<ICommand>();
        handleCollisionMock.Setup(c => c.Execute()).Verifiable();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.GetTorpedoes", (object[] args) => torpedoesList).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.CheckCollisions", (object[] args) => collisionsList).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.HandleCollision", (object[] args) => handleCollisionMock.Object).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "MoveCommand", (object[] args) => new Mock<ICommand>().Object).Execute();

        game.Update();

        handleCollisionMock.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Game_AddCommand_Should_Throw_When_Command_Is_Null()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        Assert.Throws<ArgumentNullException>(() => game.AddCommand(null!));
    }

    [Fact]
    public void Game_Constructor_Should_Throw_When_Receiver_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new Game(Guid.NewGuid(), null!));
    }

    [Fact]
    public void Game_GetState_Should_Return_State_From_IoC()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var expectedState = new { GameId = gameId, Status = "Running", Score = 100 };
        Ioc.Resolve<ICommand>("IoC.Register", "Game.State.Get", (object[] args) => expectedState).Execute();

        var state = game.GetState();

        Assert.Equal(expectedState, state);
    }

    [Fact]
    public void Game_Update_Should_Continue_On_Command_Exception()
    {
        var gameId = Guid.NewGuid();
        var commandReceiverMock = new Mock<ICommandReceiver>();
        var game = new Game(gameId, commandReceiverMock.Object);

        var failingCommand = new Mock<ICommand>();
        failingCommand.Setup(c => c.Execute()).Throws(new Exception("Test error"));

        game.AddCommand(failingCommand.Object);

        Ioc.Resolve<ICommand>("IoC.Register", "Game.GetTorpedoes", (object[] args) => new List<Guid>()).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.CheckCollisions", (object[] args) => new List<object>()).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Game.HandleCollision", (object[] args) => new Mock<ICommand>().Object).Execute();

        var exception = Record.Exception(() => game.Update());

        Assert.Null(exception); 
    }
}
