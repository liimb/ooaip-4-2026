using System;
using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyGameCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Game.Create", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            var receiver = Ioc.Resolve<ICommandReceiver>("Game.CommandReceiver");
            return new Game(gameId, receiver);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.State.Get", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            return new { GameId = gameId, Status = "Running" };
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.GetTorpedoes", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            return Ioc.Resolve<IEnumerable<Guid>>("Game.Repository.GetTorpedoes", gameId);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.CheckCollisions", (object[] args) =>
        {
            var gameId = (Guid)args[0];
            return new List<object>();
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Game.HandleCollision", (object[] args) =>
        {
            var collision = args[0];
            return new HandleCollisionCommand(collision);
        }).Execute();

        var shootCommandInstance = new object();
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Shoot", (object[] args) =>
        {
            var shootable = (IShootable)args[0];
            return new ShootCommand(shootable);
        }).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.TorpedoMove", (object[] args) =>
        {
            var torpedoId = (Guid)args[0];
            return new TorpedoMovementCommand(torpedoId);
        }).Execute();
    }
}
