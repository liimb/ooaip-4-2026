using App;
using SpaceBattle.Lib;
using Xunit;
using System;

namespace SpaceBattle.Tests
{
    class TestReceiver : ICommandReceiver
    {
        public App.ICommand? ReceivedCommand { get; private set; }
        public bool CanReceiveValue { get; set; } = true;

        public void Receive(App.ICommand command)
        {
            ReceivedCommand = command;
        }

        public bool CanReceive()
        {
            return CanReceiveValue;
        }
    }

    class TestLongCommand : App.ICommand
    {
        public bool Executed { get; private set; }

        public void Execute()
        {
            Executed = true;
        }
    }

    public class SendCommandTests
    {
        [Fact]
        public void SendCommand_LongCommand()
        {
            var longCommand = new TestLongCommand();
            var receiver = new TestReceiver();
            var sendCommand = new SendCommand(longCommand, receiver);

            sendCommand.Execute();

            Assert.Equal(longCommand, receiver.ReceivedCommand);
        }

        [Fact]
        public void SendCommand_ThrowsExceptionIfReceiverCannotAcceptLongCommmand()
        {
            var longCommand = new TestLongCommand();
            var receiver = new TestReceiver();
            receiver.CanReceiveValue = false;
            var sendCommand = new SendCommand(longCommand, receiver);

            Assert.Throws<InvalidOperationException>(() => sendCommand.Execute());
            Assert.Null(receiver.ReceivedCommand);
        }
    }
}
