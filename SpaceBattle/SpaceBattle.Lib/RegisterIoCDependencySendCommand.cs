using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencySendCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Send",
                (object[] args) =>
                {
                    var command = (App.ICommand)args[0];
                    var receiver = (ICommandReceiver)args[1];
                    return new SendCommand(command, receiver);
                }
            ).Execute();
        }
    }
}
