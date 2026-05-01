using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyRotateCommandTests
{
    public RegisterIoCDependencyRotateCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void RegisterRotateCommand_DependencyResolves()
    {
        var mockAdapter = new Mock<IRotatingObject>();

        var initialAngle = new Angle(1);
        var angularVelocity = new Angle(1);
        var expectedAngle = new Angle(2);

        mockAdapter.SetupGet(m => m.Angle).Returns(initialAngle);
        mockAdapter.SetupGet(m => m.AngularVelocity).Returns(angularVelocity);

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Adapters.IRotatingObject",
            (object[] args) => mockAdapter.Object
        ).Execute();

        var registerCommand = new RegisterIoCDependencyRotateCommand();

        registerCommand.Execute();

        var obj = new Mock<IDictionary<string, object>>().Object;
        var command = Ioc.Resolve<ICommand>("Commands.Rotate", obj);

        Assert.NotNull(command);
        Assert.IsType<RotateCommand>(command);

        command.Execute();

        mockAdapter.VerifySet(m => m.Angle = It.Is<Angle>(a => a == expectedAngle), Times.Once);
    }
}
