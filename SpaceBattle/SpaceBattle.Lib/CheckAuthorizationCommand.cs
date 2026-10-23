using App;

namespace SpaceBattle.Lib;

public class CheckAuthorizationCommand : ICommand
{
    private readonly IPrefixTree<string, bool> _tree;
    private readonly string[] _path;

    public CheckAuthorizationCommand(IPrefixTree<string, bool> tree, params string[] path)
    {
        _tree = tree;
        _path = path;
    }

    public void Execute()
    {
        var (found, value) = _tree.TryGet(_path);

        if (!found || !value)
        {
            throw new UnauthorizedAccessException($"Access denied for path: {string.Join(" -> ", _path)}");
        }
    }
}
