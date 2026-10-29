using System;
using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyRepositoryCommandTest
{
    public RegisterIoCDependencyRepositoryCommandTest()
    {
        new InitCommand().Execute();

        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        new RegisterIoCDependencyRepositoryCommand().Execute();
    }

    [Fact]
    public void RegistryGet_ShouldDelegateToFindStrategy()
    {
        var id = Guid.NewGuid();
        var expectedObj = new object();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Game.Objects.Find",
            (Func<object[], object>)(args =>
            {
                Assert.Equal(id, args[0]);
                return expectedObj;
            })
        ).Execute();

        var result = Ioc.Resolve<object>("Game.Registry.Get", id);

        Assert.Same(expectedObj, result);
    }

    [Fact]
    public void RegistryCreate_ShouldReturnCommandFromStoreStrategy()
    {
        var id = Guid.NewGuid();
        var obj = new object();
        var mockCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Game.Objects.Store",
            (Func<object[], object>)(_ => mockCommand.Object)
        ).Execute();

        var cmd = Ioc.Resolve<ICommand>("Game.Registry.Create", id, obj);

        Assert.Same(mockCommand.Object, cmd);

        cmd.Execute();

        mockCommand.Verify(x => x.Execute(), Times.Once);
    }

    [Fact]
    public void RegistryDelete_ShouldReturnCommandFromRemoveStrategy()
    {
        var id = Guid.NewGuid();
        var mockRemoveCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Game.Objects.Remove",
            (Func<object[], object>)(args =>
            {
                Assert.Equal(id, args[0]);
                return mockRemoveCommand.Object;
            })
        ).Execute();

        var cmd = Ioc.Resolve<ICommand>("Game.Registry.Delete", id);

        Assert.Same(mockRemoveCommand.Object, cmd);

        cmd.Execute();

        mockRemoveCommand.Verify(x => x.Execute(), Times.Once);
    }
}
