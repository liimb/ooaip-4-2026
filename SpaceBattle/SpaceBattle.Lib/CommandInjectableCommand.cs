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
            _command!.Execute();
        }
    }
}
