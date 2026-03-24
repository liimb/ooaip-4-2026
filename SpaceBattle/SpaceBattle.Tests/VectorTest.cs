using SpaceBattle.Lib;

namespace SpaceBattle.Tests;

public class VectorTest
{
    [Fact]
    public void Add_Vectors_ShouldReturnZeroVector()
    {
        var vector1 = new Vector([1, -1, 2]);
        var vector2 = new Vector([-1, 1, -2]);
        var expected = new Vector([0, 0, 0]);

        var result = vector1 + vector2;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Add_VectorsWithDifferentDimensions_ShouldThrow_FirstLonger()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([1, 2]);

        Assert.Throws<ArgumentException>(() => vector1 + vector2);
    }

    [Fact]
    public void Add_VectorsWithDifferentDimensions_ShouldThrow_SecondLonger()
    {
        var vector1 = new Vector([1, 2]);
        var vector2 = new Vector([1, 2, 3]);

        Assert.Throws<ArgumentException>(() => vector1 + vector2);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForEqualVectors()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([1, 2, 3]);

        Assert.True(vector1.Equals(vector2));
    }

    [Fact]
    public void OperatorEquals_ShouldReturnTrue_ForEqualVectors()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([1, 2, 3]);

        Assert.True(vector1 == vector2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_ForDifferentVectors()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([4, 5, 6]);

        Assert.False(vector1.Equals(vector2));
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnTrue_ForDifferentVectors()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([4, 5, 6]);

        Assert.True(vector1 != vector2);
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnFalse_ForEqualsVectors()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([1, 2, 3]);

        Assert.False(vector1 != vector2);
    }

    [Fact]
    public void Equals_OtherType_ReturnsFalse()
    {
        var v = new Vector([1, 2]);
        Assert.False(v.Equals("vector"));
    }

    [Fact]
    public void GetHashCode_EqualVectors_ReturnsSameHashCode()
    {
        var vector1 = new Vector([1, 2, 3]);
        var vector2 = new Vector([1, 2, 3]);

        var hashCode1 = vector1.GetHashCode();
        var hashCode2 = vector2.GetHashCode();

        Assert.Equal(hashCode1, hashCode2);
    }
}
