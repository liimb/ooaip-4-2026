using System;
using System.Collections.Generic;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyGameTests
    {
        [Fact]
        public void RegisterIoCDependencyGameCommand_RegistersAllDependencies()
        {
            new InitCommand().Execute();

            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>(
                "IoC.Scope.Current.Set",
                iocScope
            ).Execute();

            var testScope = new object();

            try
            {
                Ioc.Resolve<ICommand>(
                    "IoC.Register",
                    "Scopes.Create",
                    (Func<object[], object>)(_ => testScope)
                ).Execute();
            }
            catch (ArgumentException)
            {
            }

            try
            {
                new RegisterIoCDependencyGameCommand().Execute();
            }
            catch (ArgumentException)
            {
            }

            var canContinue = Ioc.Resolve<bool>(
                "Game.Scheduler.CanContinue"
            );

            Assert.False(canContinue);

            var take = Ioc.Resolve<object>(
                "Game.Scheduler.Take"
            );

            Assert.Null(take);

            var gameId = Guid.NewGuid();

            var game = Ioc.Resolve<Game>(
                "Game.Create",
                gameId
            );

            Assert.NotNull(game);
            Assert.IsType<Game>(game);
        }
    }
}
