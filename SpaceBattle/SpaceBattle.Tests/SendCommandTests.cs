using System;
using System.Threading;
using Xunit;
using App;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests
{
    public class SendCommandTests
    {
        class LongRunningCommand : ICommand
        {
            public void Execute()
            {
                Thread.Sleep(50);
            }
        }

        class SuccessfulReceiver : ICommandReceiver
        {
            public ICommand? ReceivedCommand;

            public void Receive(ICommand command)
            {
                ReceivedCommand = command;
            }
        }

        class FailingReceiver : ICommandReceiver
        {
            public void Receive(ICommand command)
            {
                throw new Exception("Cannot receive long-running command");
            }
        }

        [Fact]
        public void SendCommand_PassesLongRunningCommand()
        {
            var command = new LongRunningCommand();
            var receiver = new SuccessfulReceiver();
            var sendCommand = new SendCommand(command, receiver);

            sendCommand.Execute();

            Assert.Equal(command, receiver.ReceivedCommand);
        }

        [Fact]
        public void SendCommand_ThrowsException_WhenReceiverCannotAcceptLongRunningCommand()
        {
            var command = new LongRunningCommand();
            var receiver = new FailingReceiver();
            var sendCommand = new SendCommand(command, receiver);

            Assert.Throws<Exception>(() => sendCommand.Execute());
        }
    }
}
