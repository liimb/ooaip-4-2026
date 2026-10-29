using System.Collections.Generic;
using Xunit;
using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class CollisionTreeTests
{
    [Fact]
    public void CollisionTree_ContainsValidatesCorrectly()
    {
        var data = new List<(int, int, int, int)>
        {
            (1, 2, 3, 4)
        };
        var tree = new CollisionTree(data);

        Assert.False(tree.Contains(new int[] { 1, 2, 3 }));

        Assert.True(tree.Contains(new int[] { 1, 2, 3, 4 }));
        Assert.False(tree.Contains(new int[] { 0, 0, 0, 0 }));

        Assert.True(tree.Contains((1, 2, 3, 4)));
        Assert.False(tree.Contains((9, 9, 9, 9)));
    }
}
