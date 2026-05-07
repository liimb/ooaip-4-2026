using App;
using App.Scopes;
using SpaceBattle.Lib;
using Moq;
using Xunit;
using System.Collections.Generic;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStopTests
    {
        public RegisterIoCDependencyActionsStopTests()
        {
            new InitCommand().Execute();
            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
            new RegisterIoCDependencyActionsStop().Execute();
        }

        [Fact]
        public void RegisterIoCDependencyActionsStop_ResolvesDependency()
        {
            var mockCommand = new Mock<ICommand>();
            var order = new Dictionary<string, object>
            {
                { "command", mockCommand.Object }
            };

            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            Assert.NotNull(resolved);
            Assert.IsType<CommandInjectableCommand>(resolved);

            resolved.Execute();
            mockCommand.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void RegisterIoCDependencyActionsStop_ResolvesWithoutCommand()
        {
            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            Assert.NotNull(resolved);
            Assert.IsType<CommandInjectableCommand>(resolved);
        }

        [Fact]
        public void ActionStopCommand_ExecutesInConstantTime()
        {
            var mockCommand = new Mock<ICommand>();
            var order = new Dictionary<string, object>
            {
                { "command", mockCommand.Object }
            };

            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            resolved.Execute();
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 100, "ActionStopCommand should execute in constant time");
            mockCommand.Verify(c => c.Execute(), Times.Once);
        }
    }
}
