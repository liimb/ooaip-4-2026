using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace SpaceBattle.Tests
{
    public class RegisterIoCDependencyActionsStopTests
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
                    new RegisterIoCDependencyActionsStop().Execute();
                    _initialized = true;
                }
            }
        }

        [Fact]
        public void RegisterIoCDependencyActionsStop_ResolvesDependency()
        {
            Init();

            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            Assert.NotNull(resolved);
        }

        [Fact]
        public void ActionStopCommand_Constructor_ThrowsOnNullOrder()
        {
            Assert.Throws<ArgumentNullException>(() => new ActionStopCommand(null!));
        }

        [Fact]
        public void ActionStopCommand_ExecutesInConstantTime()
        {
            Init();

            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Stop", order);

            var stopwatch = Stopwatch.StartNew();
            resolved.Execute();
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 100, "ActionStopCommand should execute in constant time");
            Assert.Equal("stopped", order["status"]);
        }
    }
}
