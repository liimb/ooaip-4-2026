using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using App;

namespace SpaceBattle.Lib;

public class AdapterFactory
{
    private readonly Func<object, object> _create;

    public AdapterFactory(Func<object, object> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
    }

    public object Create(object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _create(source);
    }
}
