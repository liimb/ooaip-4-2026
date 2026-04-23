using Xunit;
using Moq;
using App;
using SpaceBattle.Lib;
using System;

namespace SpaceBattle.Tests
{
    public class SendCommandTests
    {
        [Fact]
        public void SendCommand_PassesCommandToReceiver()
        {
            var mockCommand = new Mock<App.ICommand>();
            var mockReceiver = new Mock<ICommandReceiver>();

            var sendCommand = new SendCommand(mockCommand.Object, mockReceiver.Object);
            sendCommand.Execute();

            mockReceiver.Verify(r => r.Receive(mockCommand.Object), Times.Once);
        }

        [Fact]
        public void SendCommand_Constructor_ThrowsOnNullCommand()
        {
            var receiver = new Mock<ICommandReceiver>();
            Assert.Throws<ArgumentNullException>(() => new SendCommand(null!, receiver.Object));
        }

        [Fact]
        public void SendCommand_Constructor_ThrowsOnNullReceiver()
        {
            var command = new Mock<App.ICommand>();
            Assert.Throws<ArgumentNullException>(() => new SendCommand(command.Object, null!));
        }
    }
}
