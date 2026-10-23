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
    }

    [Fact]
    public void Game_ShouldBeCreatable()
    {
        var gameId = Guid.NewGuid();
        var scope = new object();

        var game = new Game(gameId, scope);

        Assert.NotNull(game);
    }
}
