using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class GameTestIsolationTests
{
    [Fact]
    public void GameExecutionTestRestoresScopeForNextTestOnSameThread()
    {
        new InitCommand().Execute();
        var previous = Ioc.Resolve<object>("IoC.Scope.Current");
        var restore = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previous);

        try
        {
            new GameTests().GameExecution_RunsCommandsAndHandlesExceptions();
            Assert.Same(previous, Ioc.Resolve<object>("IoC.Scope.Current"));

            var scope = new GameScope();
            try
            {
                scope.Register("Test.Isolation", args => (int)args[0] + 1);
                var activation = scope.Activate();
                try
                {
                    Assert.NotSame(previous, Ioc.Resolve<object>("IoC.Scope.Current"));
                    Assert.Equal(43, Ioc.Resolve<int>("Test.Isolation", 42));
                }
                finally
                {
                    activation.Restore();
                }
            }
            finally
            {
                scope.Close();
            }

            Assert.Same(previous, Ioc.Resolve<object>("IoC.Scope.Current"));
        }
        finally
        {
            restore.Execute();
        }
    }
}
