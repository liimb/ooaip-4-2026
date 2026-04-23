using Xunit;
using Moq;
using App;
using SpaceBattle.Lib;
using System;

namespace SpaceBattle.Tests
{
	public class CommandInjectableCommandTests
	{
		[Fact]
		public void CommandInjectableCommand_ExecutesInjectedCommand()
		{
			var mockCommand = new Mock<ICommand>();
			var injectableCommand = new CommandInjectableCommand();

			injectableCommand.Inject(mockCommand.Object);
			injectableCommand.Execute();

			mockCommand.Verify(c => c.Execute(), Times.Once);
		}

		[Fact]
		public void CommandInjectableCommand_ThrowsException_WhenNoCommandInjected()
		{
			var injectableCommand = new CommandInjectableCommand();

			Assert.Throws<InvalidOperationException>(() => injectableCommand.Execute());
		}
	}
}
