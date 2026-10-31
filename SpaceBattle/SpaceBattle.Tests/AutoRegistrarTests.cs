using System;
using System.Collections.Generic;
using System.Reflection;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;
using Moq;

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

    [Fact]
    public void RegisterAll_WithInvalidInterfaces_IgnoresThem()
    {
        AutoRegistrar.RegisterAll(typeof(InvalidInterfacesPlaceholder).Assembly);
    }

    [Fact]
    public void GetTypeName_HandlesArraysAndGenerics()
    {
        var method = typeof(AutoRegistrar).GetMethod("GetTypeName",
            BindingFlags.NonPublic | BindingFlags.Static);

        var arrayName = (string)method!.Invoke(null, new object[] { typeof(int[]) })!;
        Assert.Equal("global::System.Int32[]", arrayName);

        var genericName = (string)method.Invoke(null, new object[] { typeof(List<int>) })!;
        Assert.Contains("global::System.Collections.Generic.List<global::System.Int32>", genericName);

        var normalName = (string)method.Invoke(null, new object[] { typeof(string) })!;
        Assert.Equal("global::System.String", normalName);
    }

    [Fact]
    public void RegisterAll_WithGenericInterface_IgnoresIt()
    {
        AutoRegistrar.RegisterAll(typeof(IGenericInterface<>).Assembly);
    }

    [Fact]
    public void RegisterAll_WithEmptyInterface_IgnoresIt()
    {
        AutoRegistrar.RegisterAll(typeof(IEmptyInterface).Assembly);
    }

    [Fact]
    public void RegisterCommandFactory_GeneratesCommandFromStringList()
    {
        var mockMove = new Mock<ICommand>();
        var mockRotate = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move",
            (Func<object[], object>)(_ => mockMove.Object)).Execute();
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Rotate",
            (Func<object[], object>)(_ => mockRotate.Object)).Execute();

        var command = Ioc.Resolve<ICommand>(
            "Commands.Factory.Generated",
            new object[] { new[] { "Move", "Rotate" } }
        );

        Assert.NotNull(command);
        Assert.IsType<MacroCommand>(command);

        command.Execute();

        mockMove.Verify(c => c.Execute(), Times.Once);
        mockRotate.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void RegisterCommandFactory_GeneratesSingleCommandFromString()
    {
        var mockMove = new Mock<ICommand>();
        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move",
            (Func<object[], object>)(_ => mockMove.Object)).Execute();

        var command = Ioc.Resolve<ICommand>(
            "Commands.Factory.Generated",
            new object[] { "Move" }
        );

        Assert.NotNull(command);
        Assert.Same(mockMove.Object, command);
    }
}

public class InvalidInterfacesPlaceholder { }

public interface IGenericInterface<T> { int Value { get; } }
public interface IEmptyInterface { }
public interface IIndexerInterface { int this[int index] { get; } }
public interface IWriteOnlyInterface { int Value { set; } }
public interface IMethodOnlyInterface { void DoSomething(); }
public interface IGenericPropertyInterface { List<int> Values { get; } }
