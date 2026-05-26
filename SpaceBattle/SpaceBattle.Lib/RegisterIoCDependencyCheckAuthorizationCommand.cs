using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyCheckAuthorizationCommand : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Check", (object[] args) =>
        {
            var tree = Ioc.Resolve<IPrefixTree<string, bool>>("Authorization.Tree");
            
            var path = args.Cast<string>().ToArray();
            
            return new CheckAuthorizationCommand(tree, path);
            
        }).Execute();
    }
}
