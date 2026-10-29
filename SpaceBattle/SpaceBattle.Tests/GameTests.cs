using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTests
{
    public GameTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void GameExecution_RunsCommandsAndHandlesExceptions()
    {
        var gameId = Guid.NewGuid();
        var scope = new object();

        var setScopeCommandMock = new Mock<ICommand>();
        var normalCommandMock = new Mock<ICommand>();
        var exceptionCommandMock = new Mock<ICommand>();
        var exceptionHandlerMock = new Mock<ICommand>();

        var canContinueSequence = new Queue<bool>(new[] { true, true, false });

        Ioc.Resolve<ICommand>("IoC.Register", "IoC.Scope.Current.Set", (object[] args) => setScopeCommandMock.Object).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Scheduler.CanContinue", (object[] args) => (object)canContinueSequence.Dequeue()).Execute();

        var scheduledCommands = new Queue<ICommand>(new[] {
            normalCommandMock.Object,
            exceptionCommandMock.Object
        });
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Scheduler.Take", (object[] args) => (object)scheduledCommands.Dequeue()).Execute();

        var thrownException = new Exception("Test exception");
        exceptionCommandMock.Setup(c => c.Execute()).Throws(thrownException);

        Ioc.Resolve<ICommand>("IoC.Register", "ExceptionHandler.Handle", (object[] args) => {
            Assert.Equal(exceptionCommandMock.Object, args[0]);
            Assert.Equal(thrownException, args[1]);
            return exceptionHandlerMock.Object;
        }).Execute();

        var game = new Game(gameId, scope);

        game.Execute();

        setScopeCommandMock.Verify(c => c.Execute(), Times.Once);
        normalCommandMock.Verify(c => c.Execute(), Times.Once);
        exceptionCommandMock.Verify(c => c.Execute(), Times.Once);
        exceptionHandlerMock.Verify(c => c.Execute(), Times.Once);
    }
}
