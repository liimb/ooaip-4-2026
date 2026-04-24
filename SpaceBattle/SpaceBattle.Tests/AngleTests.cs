using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class AngleTests
{
    [Fact]
    public void AddAngles_5_8_And_7_8_Returns_4_8()
    {
        var angle1 = new Angle(5);
        var angle2 = new Angle(7);
        var result = angle1 + angle2;
        var expected = new Angle(4);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AddAngles_WrapsAround_WhenSumExceedsDenominator()
    {
        var angle1 = new Angle(5);
        var angle2 = new Angle(4);
        var result = angle1 + angle2;
        var expected = new Angle(1);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(15, 23, true)]
    [InlineData(1, 2, false)]
    public void EqualityTests(int num1, int num2, bool expectedEqual)
    {
        var angle1 = new Angle(num1);
        var angle2 = new Angle(num2);

        Assert.Equal(expectedEqual, angle1.Equals(angle2));
        Assert.Equal(expectedEqual, angle1 == angle2);
        Assert.Equal(!expectedEqual, angle1 != angle2);
    }

    [Theory]
    [InlineData(5, "5/8π")]
    [InlineData(3, "3/8π")]
    public void ToString_ReturnsCorrectFormat(int numerator, string expected)
    {
        var angle = new Angle(numerator);
        Assert.Equal(expected, angle.ToString());
    }

    [Theory]
    [InlineData(2, Math.PI / 2)]
    [InlineData(4, Math.PI)]
    [InlineData(-2, 3 * Math.PI / 2)]
    public void ToRadians_ReturnsCorrectValue(int numerator, double expected)
    {
        var angle = new Angle(numerator);
        Assert.Equal(expected, angle.ToRadians(), 0.0001);
    }

    [Fact]
    public void MathCos_CanBeCalledWithAngle()
    {
        var angle = new Angle(2);
        Assert.Equal(0, Math.Cos(angle), 0.0001);
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(-3, 5)]
    [InlineData(-5, 3)]
    public void Constructor_NormalizesValues(int input, int expectedNum)
    {
        var angle = new Angle(input);
        var expected = new Angle(expectedNum);
        Assert.Equal(expected, angle);
    }

    [Fact]
    public void GetHashCode_IsConsistent()
    {
        var angle1 = new Angle(5);
        var angle2 = new Angle(5);
        var angle3 = new Angle(-3);
        var angle4 = new Angle(5);

        Assert.Equal(angle1.GetHashCode(), angle2.GetHashCode());
        Assert.Equal(angle3.GetHashCode(), angle4.GetHashCode());
        Assert.NotEqual(0, angle1.GetHashCode());

        var angle5 = new Angle(1);
        var angle6 = new Angle(2);
        Assert.NotEqual(angle5.GetHashCode(), angle6.GetHashCode());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("not an angle", false)]
    public void Equals_WithNonAngleObject_ReturnsFalse(object? obj, bool expected)
    {
        var angle = new Angle(5);
        Assert.Equal(expected, angle.Equals(obj));
    }

    [Fact]
    public void ImplicitConversionToDouble_ReturnsRadians()
    {
        var angle = new Angle(2);
        double radians = angle;
        Assert.Equal(Math.PI / 2, radians, 0.0001);
    }
}
