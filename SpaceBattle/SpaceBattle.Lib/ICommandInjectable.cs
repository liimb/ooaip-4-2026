<<<<<<< HEAD
=======
using System;
>>>>>>> 17f869b (feat: add CommandInjectableCommand with tests)
using App;

namespace SpaceBattle.Lib
{
    public interface ICommandInjectable
    {
        void Inject(ICommand command);
    }
}
