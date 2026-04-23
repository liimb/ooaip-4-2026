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
                    return new StartActionCommand(order);
                }
            ).Execute();
        }
    }
}
