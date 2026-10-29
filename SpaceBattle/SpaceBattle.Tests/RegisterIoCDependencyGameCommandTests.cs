
using System;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyGameTests
    {
        public RegisterIoCDependencyGameTests()
        {
            new InitCommand().Execute();

            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

            new RegisterIoCDependencyGameCommand().Execute();
        }

        [Fact]
        public void GameCreate_ResolvesDependency()
        {
            var gameId = Guid.NewGuid();

            var testScope = new object();

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Scopes.Create",
                (Func<object[], object>)(_ => testScope)
            ).Execute();

            var game = Ioc.Resolve<Game>("Game.Create", gameId);

            Assert.NotNull(game);
            Assert.IsType<Game>(game);
        }

        [Fact]
        public void GameSchedulerCanContinue_ReturnsFalse()
        {
            var result = Ioc.Resolve<bool>("Game.Scheduler.CanContinue");

            Assert.False(result);
        }

        [Fact]
        public void GameSchedulerTake_ReturnsNull()
        {
            var result = Ioc.Resolve<object>("Game.Scheduler.Take");

            Assert.Null(result);
        }
    }
}
