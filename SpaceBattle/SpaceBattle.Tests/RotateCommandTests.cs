using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class RotateCommandTests
{
    [Fact]
    public void Rotate_WithAngle45AndAngularVelocity45_Returns90()
    {
        var angle45 = new Angle(1);
        var velocity45 = new Angle(1);
        var expectedAngle90 = new Angle(2);

        var mock = new Mock<IRotatingObject>();
        mock.SetupGet(m => m.Angle).Returns(angle45);
        mock.SetupGet(m => m.AngularVelocity).Returns(velocity45);

        var command = new RotateCommand(mock.Object);
        command.Execute();

        mock.VerifySet(m => m.Angle = It.Is<Angle>(a => a == expectedAngle90), Times.Once);
    }

    [Fact]
    public void Rotate_WhenCannotGetAngle_ThrowsException()
    {
        var mock = new Mock<IRotatingObject>();
        mock.SetupGet(m => m.Angle).Throws<KeyNotFoundException>();

        var command = new RotateCommand(mock.Object);

        Assert.Throws<KeyNotFoundException>(() => command.Execute());
    }

    [Fact]
    public void Rotate_WhenCannotGetAngularVelocity_ThrowsException()
    {
        var mock = new Mock<IRotatingObject>();
        mock.SetupGet(m => m.Angle).Returns(new Angle(0));
        mock.SetupGet(m => m.AngularVelocity).Throws<KeyNotFoundException>();

        var command = new RotateCommand(mock.Object);

        Assert.Throws<KeyNotFoundException>(() => command.Execute());
    }

    [Fact]
    public void Rotate_WhenCannotSetAngle_ThrowsException()
    {
        var mock = new Mock<IRotatingObject>();
        mock.SetupGet(m => m.Angle).Returns(new Angle(0));
        mock.SetupGet(m => m.AngularVelocity).Returns(new Angle(0));
        mock.SetupSet(m => m.Angle = It.IsAny<Angle>()).Throws<KeyNotFoundException>();

        var command = new RotateCommand(mock.Object);

        Assert.Throws<KeyNotFoundException>(() => command.Execute());
    }
}
