using App;
using App.Scopes;
using SpaceBattle.Lib;
using Moq;
using Xunit;
using System;
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
            try
            {
                new RegisterIoCDependencyActionsStart().Execute();
            }
            catch (ArgumentException)
            {
            }
        }

        [Fact]
        public void RegisterIoCDependencyActionsStart_ResolvesDependency()
        {
            var mockCommand = new Mock<ICommand>();
            var order = new Dictionary<string, object>
            {
                { "command", mockCommand.Object }
            };

            var resolved = Ioc.Resolve<ICommand>("Actions.Start", order);

            Assert.NotNull(resolved);
            Assert.IsType<CommandInjectableCommand>(resolved);

            resolved.Execute();
            mockCommand.Verify(c => c.Execute(), Times.Once);
        }

        [Fact]
        public void RegisterIoCDependencyActionsStart_ResolvesWithoutCommand()
        {
            var order = new Dictionary<string, object>();
            var resolved = Ioc.Resolve<ICommand>("Actions.Start", order);

            Assert.NotNull(resolved);
            Assert.IsType<CommandInjectableCommand>(resolved);
        }
    }
}