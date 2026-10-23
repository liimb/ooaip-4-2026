using App;
using System.Collections.Generic;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyRotateCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Rotate",
            (object[] args) =>
            {
                var obj = (IDictionary<string, object>)args[0];
                var adapter = Ioc.Resolve<IRotatingObject>("Adapters.IRotatingObject", obj);
                return new RotateCommand(adapter);
            }
        ).Execute();
    }
}
