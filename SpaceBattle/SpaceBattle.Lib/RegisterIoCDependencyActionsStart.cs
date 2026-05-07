using System;
using System.Collections.Generic;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyActionsStart : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Actions.Start",
                (object[] args) =>
                {
                    var order = (IDictionary<string, object>)args[0];
                    var injectableCommand = new CommandInjectableCommand();

                    if (order.TryGetValue("command", out var cmdObj) && cmdObj is ICommand cmd)
                    {
                        injectableCommand.Inject(cmd);
                    }

                    return injectableCommand;
                }
            ).Execute();
        }
    }
}
