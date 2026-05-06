using App;
using App.Scopes;
using SpaceBattle.Lib;
using Moq;
using Xunit;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencySendCommandTests
    {
        public RegisterIoCDependencySendCommandTests()
        {
            new InitCommand().Execute();
            var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
            new RegisterIoCDependencySendCommand().Execute();
        }

        [Fact]
        public void RegisterDependencySendCommand_DependencyResolves()
        {
            var cmd = new Mock<ICommand>();
            var receiver = new Mock<ICommandReceiver>();

            var resolved = Ioc.Resolve<ICommand>(
                "Commands.Send",
                cmd.Object,
                receiver.Object);

            Assert.IsType<SendCommand>(resolved);
        }
    }
}
