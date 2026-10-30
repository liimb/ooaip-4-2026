using System.Collections.Generic;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class SetPropertyCommandTests
{
    [Fact]
    public void Execute_UpdatesDictionaryValue()
    {
        var oldPosition = new Vector(new[] { 0, 0 });
        var newPosition = new Vector(new[] { 1, 2 });
        var data = new Dictionary<string, object>
        {
            ["Location"] = oldPosition
        };

        var command = new SetPropertyCommand(data, "Location", newPosition);
        command.Execute();

        Assert.Same(newPosition, data["Location"]);
    }
}
