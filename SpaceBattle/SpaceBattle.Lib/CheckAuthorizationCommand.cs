using App;

namespace SpaceBattle.Lib;

public class CheckAuthorizationCommand(params string[] path) : ICommand
{
    private readonly string[] _path = path;

    public void Execute()
    {
        var tree = Ioc.Resolve<PrefixTree<string, bool>>("Authorization.Tree");

        var (Found, Value) = tree.TryGet(_path);

        if (!Found || !Value)
        {
            throw new UnauthorizedAccessException($"Access denied for path: {string.Join(" -> ", _path)}");
        }
    }
}
