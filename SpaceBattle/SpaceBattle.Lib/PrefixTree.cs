namespace SpaceBattle.Lib;

public class PrefixTree<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, PrefixTree<TKey, TValue>> _children = [];
    public TValue? Value { get; set; }
    public bool IsTerminal { get; set; }

    public void Add(IEnumerable<TKey> sequence, TValue value)
    {
        var current = this;
        foreach (var key in sequence)
        {
            if (!current._children.TryGetValue(key, out var child))
            {
                child = new PrefixTree<TKey, TValue>();
                current._children[key] = child;
            }
            current = child;
        }
        current.Value = value;
        current.IsTerminal = true;
    }

    public (bool Found, TValue? Value) TryGet(IEnumerable<TKey> sequence)
    {
        var current = this;
        foreach (var key in sequence)
        {
            if (!current._children.TryGetValue(key, out var child))
                return (false, default);
            current = child;
        }
        return (current.IsTerminal, current.Value);
    }
}
