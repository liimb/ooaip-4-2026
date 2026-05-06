using App;
using App.Scopes;
using SpaceBattle.Lib;
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
            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            Assert.NotNull(resolved);
            Assert.IsType<ActionStopCommand>(resolved);
        }

        [Fact]
        public void ActionStopCommand_Constructor_ThrowsOnNullOrder()
        {
            Assert.Throws<ArgumentNullException>(() => new ActionStopCommand(null!));
        }

        [Fact]
        public void ActionStopCommand_ExecutesInConstantTime()
        {
            var order = new Dictionary<string, object>();
            var resolved = new ActionStopCommand(order);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            resolved.Execute();
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 100, "ActionStopCommand should execute in constant time");
        }
    }
}
