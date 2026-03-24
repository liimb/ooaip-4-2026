using App;
namespace SpaceBattle.Lib;

public class RegisterIoCDependencyMoveCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.Move",
            (object[] args) =>
            {
                var obj = args[0];
                var moving = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", obj);
                return new MoveCommand(moving);
            }
        ).Execute();
    }
}
