using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterDependencyCommandInjectableCommandTests
    {
        public RegisterDependencyCommandInjectableCommandTests()
        {
            new InitCommand().Execute();
            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
            new RegisterDependencyCommandInjectableCommand().Execute();
        }

        [Fact]
        public void Resolve_CommandInjectableCommand_Works_For_All_Types()
        {
            var cmd1 = Ioc.Resolve<ICommand>("Commands.CommandInjectable");
            var cmd2 = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");
            var cmd3 = Ioc.Resolve<CommandInjectableCommand>("Commands.CommandInjectable");

            Assert.NotNull(cmd1);
            Assert.NotNull(cmd2);
            Assert.NotNull(cmd3);

            Assert.IsType<CommandInjectableCommand>(cmd1);
            Assert.IsType<CommandInjectableCommand>(cmd2);
            Assert.IsType<CommandInjectableCommand>(cmd3);
        }
    }
}
