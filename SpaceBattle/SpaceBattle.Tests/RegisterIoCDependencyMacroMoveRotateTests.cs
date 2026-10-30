using System;
using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyMacroMoveRotateTests
{
    public RegisterIoCDependencyMacroMoveRotateTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void RegisterMacroMove_DependencyResolves()
    {
        var commandNames = new[] { "MoveCmd1", "MoveCmd2" };

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Specs.Move",
                (object[] args) => commandNames
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.MoveCmd1",
                (object[] args) => mock1.Object
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.MoveCmd2",
                (object[] args) => mock2.Object
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        var registerCommand = new RegisterIoCDependencyMacroMoveRotate();
        try
        {
            registerCommand.Execute();
        }
        catch (ArgumentException)
        {
        }

        var macroCommand = Ioc.Resolve<ICommand>("Macro.Move");

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mock1.Verify(x => x.Execute(), Times.Once);
        mock2.Verify(x => x.Execute(), Times.Once);
    }

    [Fact]
    public void RegisterMacroRotate_DependencyResolves()
    {
        var commandNames = new[] { "RotateCmd1", "RotateCmd2" };

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Specs.Rotate",
                (object[] args) => commandNames
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.RotateCmd1",
                (object[] args) => mock1.Object
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        try
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.RotateCmd2",
                (object[] args) => mock2.Object
            ).Execute();
        }
        catch (ArgumentException)
        {
        }

        var registerCommand = new RegisterIoCDependencyMacroMoveRotate();
        try
        {
            registerCommand.Execute();
        }
        catch (ArgumentException)
        {
        }

        var macroCommand = Ioc.Resolve<ICommand>("Macro.Rotate");

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mock1.Verify(x => x.Execute(), Times.Once);
        mock2.Verify(x => x.Execute(), Times.Once);
    }
}