using System;
using System.Threading;
using Xunit;
using Moq;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class SendCommandTests
    {
        [Fact]
        public void SendCommand_PassesLongRunningCommand_ToReceiver()
        {
            var mockCommand = new Mock<ICommand>();

            mockCommand.Setup(c => c.Execute()).Callback(() =>
            {
                Thread.Sleep(50);
            });

            var mockReceiver = new Mock<ICommandReceiver>();

            var sendCommand = new SendCommand(mockCommand.Object, mockReceiver.Object);

            sendCommand.Execute();

            mockReceiver.Verify(r => r.Receive(mockCommand.Object), Times.Once);
        }

        [Fact]
        public void SendCommand_ThrowsException_WhenReceiverCannotAcceptLongCommand()
        {
            var mockCommand = new Mock<ICommand>();

            var mockReceiver = new Mock<ICommandReceiver>();

            mockReceiver
                .Setup(r => r.Receive(mockCommand.Object))
                .Throws(new InvalidOperationException("Cannot receive long command"));

            var sendCommand = new SendCommand(mockCommand.Object, mockReceiver.Object);

            Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());
        }
    }
}
