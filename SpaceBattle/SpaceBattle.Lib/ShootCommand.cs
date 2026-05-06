using App;

namespace SpaceBattle.Lib;

public class ShootCommand : ICommand
{
    private readonly IShootable _shootable;

    public ShootCommand(IShootable shootable)
    {
        _shootable = shootable;
    }

    public void Execute()
    {
        var cmd = Ioc.Resolve<ICommand>("Game.Operations.Shooting", _shootable);
        cmd.Execute();
    }
}
