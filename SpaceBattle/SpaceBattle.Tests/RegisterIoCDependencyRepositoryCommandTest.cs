using SpaceBattle.Lib;
using Moq;
using App;
using App.Scopes;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyRepositoryCommandTest
{
    public RegisterIoCDependencyRepositoryCommandTest()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void RegistryGet_ShouldDelegateToFindStrategy()
    {
        var id = Guid.NewGuid();
        var expectedObj = new object();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Find", (object[] args) =>
        {
            if ((Guid)args[0] == id) return expectedObj;
            throw new Exception();
        }).Execute();

        new RegisterIoCDependencyRepositoryCommand().Execute();

        var result = Ioc.Resolve<object>("Game.Registry.Get", id);
        Assert.Same(expectedObj, result);
    }

    [Fact]
    public void RegistryCreate_ShouldReturnCommandFromStoreStrategy()
    {
        var id = Guid.NewGuid();
        var obj = new object();
        var mockCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Store", (object[] args) =>
        {
            return mockCommand.Object;
        }).Execute();

        new RegisterIoCDependencyRepositoryCommand().Execute();

        var cmd = Ioc.Resolve<ICommand>("Game.Registry.Create", id, obj);
        cmd.Execute();

        mockCommand.Verify(x => x.Execute(), Times.Once);
    }

    [Fact]
    public void RegistryDelete_ShouldReturnCommandFromRemoveStrategy()
    {
        var id = Guid.NewGuid();
        var mockRemoveCommand = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.Objects.Remove", (object[] args) =>
        {
            if ((Guid)args[0] == id) return mockRemoveCommand.Object;
            throw new Exception("Wrong ID passed to Remove strategy");
        }).Execute();

        new RegisterIoCDependencyRepositoryCommand().Execute();

        var cmd = Ioc.Resolve<ICommand>("Game.Registry.Delete", id);
        cmd.Execute();

        mockRemoveCommand.Verify(x => x.Execute(), Times.Once);
    }
}
