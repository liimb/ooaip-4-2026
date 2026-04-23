using App;
using App.Scopes;
using SpaceBattle.Lib;
using Moq;
using Xunit;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencySendCommandTests
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
                    new RegisterIoCDependencySendCommand().Execute();
                    _initialized = true;
                }
            }
        }

        [Fact]
        public void RegisterDependencySendCommand_DependencyResolves()
        {
            Init();

            var cmd = new Mock<App.ICommand>();
            var receiver = new Mock<ICommandReceiver>();

            var resolved = Ioc.Resolve<App.ICommand>(
                "Commands.Send",
                cmd.Object,
                receiver.Object);

            Assert.IsType<SendCommand>(resolved);
        }

        [Fact]
        public void RegisterDependencySendCommand_WorksInMultipleThreads()
        {
            Init(); 

            var cmd = new Mock<App.ICommand>();
            var receiver = new Mock<ICommandReceiver>();

            var results = new ConcurrentBag<App.ICommand>();

            Parallel.For(0, 20, _ =>
            {
                results.Add(Ioc.Resolve<App.ICommand>(
                    "Commands.Send",
                    cmd.Object,
                    receiver.Object));
            });

            Assert.All(results, r =>
                Assert.IsType<SendCommand>(r));
        }
    }
}
