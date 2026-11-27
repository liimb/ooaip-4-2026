using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyGameTests
{
    [Fact]
    public void RegisterIoCDependencyGameCommand_RegistersAllDependencies()
    {
        var scope = new GameScope();
        var server = new GameServer(new ServerOptions());
        try
        {
            var activation = scope.Activate();
            try
            {
                new RegisterIoCDependencyGameCommand().Execute();
                var gameId = Guid.NewGuid();
                var game = Ioc.Resolve<Game>("Game.Create", gameId, server);
                Assert.Equal(gameId, game.Id);
                Assert.Equal(GameState.Created, game.State);
                var command = new Moq.Mock<ICommand>();
                Assert.Equal(DispatchResult.Accepted, server.Dispatcher.Dispatch(gameId, command.Object));
                server.Stop();
                command.Verify(item => item.Execute(), Moq.Times.Once);
                Assert.Equal(GameState.Completed, game.State);
            }
            finally
            {
                activation.Restore();
            }
        }
        finally
        {
            server.Stop();
            scope.Close();
        }
    }
}
