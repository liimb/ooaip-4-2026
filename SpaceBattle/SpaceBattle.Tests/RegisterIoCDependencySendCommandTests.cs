using App;
using SpaceBattle.Lib;
using Moq;
using Xunit;
using App.Scopes;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencySendCommandTests
    {
        public RegisterIoCDependencySendCommandTests()
        {
            new InitCommand().Execute();
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
