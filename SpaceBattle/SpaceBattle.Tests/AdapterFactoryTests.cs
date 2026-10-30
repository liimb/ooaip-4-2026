using System;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class AdapterFactoryTests
{
    private sealed class TestSource
    {
        public int Value { get; set; }
    }

    private sealed class TestAdapter
    {
        public object Source { get; }
        public TestAdapter(object source) { Source = source; }
    }

    [Fact]
    public void Create_ReturnsObjectProducedByDelegate()
    {
        var source = new TestSource();
        var expected = new TestAdapter(source);
        var factory = new AdapterFactory(_ => expected);

        var result = factory.Create(source);

        Assert.Same(expected, result);
    }

    [Fact]
    public void Create_PassesSourceToDelegate()
    {
        var source = new TestSource();
        object? received = null;
        var factory = new AdapterFactory(value =>
        {
            received = value;
            return new TestAdapter(value);
        });

        factory.Create(source);

        Assert.Same(source, received);
    }

    [Fact]
    public void Constructor_ThrowsWhenDelegateIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AdapterFactory(null!));
    }

    [Fact]
    public void Create_PropagatesDelegateException()
    {
        var factory = new AdapterFactory(
            _ => throw new InvalidOperationException()
        );

        Assert.Throws<InvalidOperationException>(
            () => factory.Create(new TestSource())
        );
    }
}
