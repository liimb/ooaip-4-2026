using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Create", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            var server = (GameServer)args[1];
            var configure = args.Length > 2 ? (Action<GameScope>)args[2] : null;
            return server.CreateGame(gameId, configure);
        }).Execute();
    }
}
