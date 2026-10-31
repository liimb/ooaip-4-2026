using System;
using System.Collections.Generic;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class PropertyAdapterFactoryTests
{
    [Fact]
    public void Get_ReturnsValueFromDictionary()
    {
        var values = new Dictionary<string, object> { ["Count"] = 42 };
        var result = PropertyAdapterFactory.Get<int>(values, "Count");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Get_ReturnsReferenceTypeValue()
    {
        var expected = "value";
        var values = new Dictionary<string, object> { ["Name"] = expected };
        var result = PropertyAdapterFactory.Get<string>(values, "Name");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Get_ThrowsWhenDictionaryIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => PropertyAdapterFactory.Get<int>(null!, "Count")
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Get_ThrowsWhenKeyIsEmpty(string key)
    {
        var values = new Dictionary<string, object>();
        Assert.Throws<ArgumentException>(
            () => PropertyAdapterFactory.Get<int>(values, key)
        );
    }

    [Fact]
    public void Get_ThrowsWhenKeyDoesNotExist()
    {
        var values = new Dictionary<string, object>();
        Assert.Throws<KeyNotFoundException>(
            () => PropertyAdapterFactory.Get<int>(values, "Count")
        );
    }

    [Fact]
    public void Get_ThrowsWhenTypeIsIncorrect()
    {
        var values = new Dictionary<string, object> { ["Count"] = "not a number" };
        Assert.Throws<InvalidCastException>(
            () => PropertyAdapterFactory.Get<int>(values, "Count")
        );
    }

    [Fact]
    public void CreateSetCommand_ReturnsCommand()
    {
        var values = new Dictionary<string, object> { ["Count"] = 1 };
        var command = PropertyAdapterFactory.CreateSetCommand(values, "Count", 10);
        Assert.NotNull(command);
    }

    [Fact]
    public void CreateSetCommand_UpdatesDictionaryWhenExecuted()
    {
        var values = new Dictionary<string, object> { ["Count"] = 1 };
        var command = PropertyAdapterFactory.CreateSetCommand(values, "Count", 10);
        command.Execute();
        Assert.Equal(10, values["Count"]);
    }
}
