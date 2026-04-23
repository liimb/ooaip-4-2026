using System;
using App;

namespace SpaceBattle.Lib
{
    public class CommandInjectableCommand : ICommand, ICommandInjectable
    {
        private ICommand? _command;

        public void Inject(ICommand command)
        {
            _command = command;
        }

        public void Execute()
        {
            if (_command == null)
            {
                throw new InvalidOperationException("Command not injected");
            }
            _command.Execute();
        }
    }
}
