using App;
using Xunit;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyGameCommandTests
{
    [Fact]
    public void Execute_RegistersGameCreate_AndFactoryCreatesGame()
    {
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Scopes.Create",
            (Func<object[], object>)(_ => new object())
        ).Execute();

        var cmd = new RegisterIoCDependencyGameCommand();
        cmd.Execute();

        var gameId = Guid.NewGuid();
        var game = Ioc.Resolve<Game>("Game.Create", gameId);

        Assert.NotNull(game);
    }

    [Fact]
    public void Execute_RegistersSchedulerCanContinue_ReturnsFalse()
    {
        var cmd = new RegisterIoCDependencyGameCommand();
        cmd.Execute();

        var canContinue = Ioc.Resolve<object>("Game.Scheduler.CanContinue");

        Assert.Equal(false, canContinue);
    }

    [Fact]
    public void Execute_RegistersSchedulerTake_ReturnsNull()
    {
        var cmd = new RegisterIoCDependencyGameCommand();
        cmd.Execute();

        var take = Ioc.Resolve<object>("Game.Scheduler.Take");

        Assert.Null(take);
    }
}
