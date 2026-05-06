using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;
using System.Collections.Generic;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStartTests
    {
        public RegisterIoCDependencyActionsStartTests()
        {
            new InitCommand().Execute();
            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
            new RegisterIoCDependencyActionsStart().Execute();
        }

        [Fact]
        public void RegisterIoCDependencyActionsStart_ResolvesActionStartCommand()
        {
            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Start", order);

            Assert.NotNull(resolved);
            Assert.IsType<StartActionCommand>(resolved);
        }
    }
}
