namespace SpaceBattle.Lib;

public static class PropertyAdapterFactory
{
    public static T Get<T>(
        IDictionary<string, object> values,
        string key)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!values.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException(
                $"В словаре отсутствует ключ '{key}'.");
        }

        if (value is T result)
        {
            return result;
        }

        throw new InvalidCastException(
            $"Значение по ключу '{key}' не имеет типа {typeof(T).FullName}.");
    }

    public static SetPropertyCommand CreateSetCommand(
        IDictionary<string, object> values,
        string key,
        object value)
    {
        return new SetPropertyCommand(values, key, value);
    }
}
