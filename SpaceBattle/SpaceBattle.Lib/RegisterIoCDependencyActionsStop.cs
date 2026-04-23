using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyActionsStop : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Actions.Stop",
                (object[] args) =>
                {
                    var order = (IDictionary<string, object>)args[0];
                    return new ActionStopCommand(order);
                }
            ).Execute();
        }
    }
}
