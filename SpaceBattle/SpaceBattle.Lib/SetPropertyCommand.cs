using App;

namespace SpaceBattle.Lib;

public class SetPropertyCommand : ICommand
{
    private readonly IDictionary<string, object> _source;
    private readonly string _key;
    private readonly object _value;

    public SetPropertyCommand(
        IDictionary<string, object> source,
        string key,
        object value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _source = source;
        _key = key;
        _value = value;
    }

    public void Execute()
    {
        _source[_key] = _value;
    }
}
