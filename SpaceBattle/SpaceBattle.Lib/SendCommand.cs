using App;
using System;

namespace SpaceBattle.Lib
{
    public class SendCommand : App.ICommand
    {
        private App.ICommand _command;
        private ICommandReceiver _receiver;

        public SendCommand(App.ICommand command, ICommandReceiver receiver)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }
            _command = command;
            _receiver = receiver;
        }

        public void Execute()
        {
            if (!_receiver.CanReceive())
            {
                throw new InvalidOperationException("Receiver cannot accept command");
            }
            _receiver.Receive(_command);
        }
    }
}
