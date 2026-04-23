using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;
using System.Collections.Generic;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStartTests
    {
        [Fact]
        public void RegisterIoCDependencyActionsStart_ResolvesActionStartCommand()
        {
            new InitCommand().Execute();
            new RegisterIoCDependencyActionsStart().Execute();

            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Start", order);

            Assert.NotNull(resolved);
            Assert.IsType<StartActionCommand>(resolved);

            resolved.Execute();
            Assert.Equal("started", order["status"]);
        }

        [Fact]
        public void ActionStartCommand_Constructor_ThrowsOnNullOrder()
        {
            Assert.Throws<ArgumentNullException>(() => new StartActionCommand(null!));
        }
    }
}
