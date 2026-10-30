using System;
using System.Collections.Generic;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class AutoRegistrarTests : IDisposable
{
    private readonly object _previousScope;

    public AutoRegistrarTests()
    {
        new InitCommand().Execute();
        _previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        AutoRegistrar.RegisterAll(typeof(IMovingObject).Assembly);
    }

    public void Dispose()
    {
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _previousScope).Execute();
    }

    [Fact]
    public void RegisterAdapter_ResolvesMovingObject()
    {
        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);

        Assert.NotNull(adapter);
        Assert.True(adapter.Position == new Vector(new[] { 0, 0 }));
        Assert.True(adapter.Velocity == new Vector(new[] { 1, 0 }));
    }

    [Fact]
    public void MoveCommand_ChangesPosition()
    {
        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);
        new MoveCommand(adapter).Execute();

        Assert.Equal(new Vector(new[] { 1, 0 }), data["Position"]);
    }

    [Fact]
    public void RegisterAdapter_PositionSetterUpdatesSourceDictionary()
    {
        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);
        var newPosition = new Vector(new[] { 5, 7 });
        adapter.Position = newPosition;

        Assert.Equal(newPosition, data["Position"]);
    }

    [Fact]
    public void RegisterAdapter_UsesCurrentDictionaryValues()
    {
        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 1, 2 }),
            ["Velocity"] = new Vector(new[] { 2, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);

        Assert.Equal(new Vector(new[] { 1, 2 }), adapter.Position);
        Assert.Equal(new Vector(new[] { 2, 0 }), adapter.Velocity);

        data["Position"] = new Vector(new[] { 3, 4 });
        Assert.Equal(new Vector(new[] { 3, 4 }), adapter.Position);
    }

    [Fact]
    public void RegisterAdapter_RepeatedRegistrationDoesNotBreakResolution()
    {
        AutoRegistrar.RegisterAll(typeof(IMovingObject).Assembly);

        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);

        Assert.NotNull(adapter);
        Assert.Equal(new Vector(new[] { 0, 0 }), adapter.Position);
    }

    [Fact]
    public void RegisterAll_NullAssembly_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => AutoRegistrar.RegisterAll(null!)
        );
    }

    [Fact]
    public void RegisterAll_Twice_DoesNotThrow()
    {
        AutoRegistrar.RegisterAll(typeof(IMovingObject).Assembly);
        AutoRegistrar.RegisterAll(typeof(IMovingObject).Assembly);

        var data = new Dictionary<string, object>
        {
            ["Position"] = new Vector(new[] { 0, 0 }),
            ["Velocity"] = new Vector(new[] { 1, 0 })
        };

        var adapter = Ioc.Resolve<IMovingObject>("Adapters.IMovingObject", data);
        Assert.NotNull(adapter);
    }
    [Fact]
    public void ResolveAdapter_ThrowsWhenNoArgs()
    {
        Assert.Throws<ArgumentException>(
            () => Ioc.Resolve<IMovingObject>("Adapters.IMovingObject")
        );
    }

    [Fact]
    public void ResolveAdapter_ThrowsWhenArgIsNull()
    {
        Assert.Throws<ArgumentException>(
            () => Ioc.Resolve<IMovingObject>(
                "Adapters.IMovingObject",
                new object[] { null! })
        );
    }
}
