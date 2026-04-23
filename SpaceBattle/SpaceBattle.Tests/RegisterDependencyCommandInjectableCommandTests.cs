using App;
using SpaceBattle.Lib;
using Xunit;
using App.Scopes;

namespace SpaceBattle.Tests
{
    public class RegisterDependencyCommandInjectableCommandTests
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        private void Init()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    new InitCommand().Execute();
                    new RegisterDependencyCommandInjectableCommand().Execute();
                    _initialized = true;
                }
            }
        }

        [Fact]
        public void Resolve_CommandInjectableCommand_Works_For_All_Types()
        {
            Init();

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
