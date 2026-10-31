using System;
using System.Collections.Generic;
using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyMacroCommandTests
{
    public RegisterIoCDependencyMacroCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        try
        {
            new RegisterIoCDependencyMacroCommand().Execute();
        }
        catch (ArgumentException)
        {
        }
    }

    [Fact]
    public void RegisterMacroCommand_WithNoArgs_ReturnsEmptyMacroCommand()
    {
        var macroCommand = Ioc.Resolve<ICommand>("Commands.Macro");

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        var exception = Record.Exception(() => macroCommand.Execute());
        Assert.Null(exception);
    }

    [Fact]
    public void RegisterMacroCommand_WithStringNames_ResolvesAndExecutesCommands()
    {
        var mockMove = new Mock<ICommand>();
        var mockRotate = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move",
            (Func<object[], object>)(_ => mockMove.Object)).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Rotate",
            (Func<object[], object>)(_ => mockRotate.Object)).Execute();

        var macroCommand = Ioc.Resolve<ICommand>(
            "Commands.Macro",
            new object[] { new[] { "Move", "Rotate" } }
        );

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mockMove.Verify(c => c.Execute(), Times.Once);
        mockRotate.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void RegisterMacroCommand_WithStringNames_IgnoresNullOrWhiteSpace()
    {
        var mockMove = new Mock<ICommand>();

        Ioc.Resolve<ICommand>("IoC.Register", "Commands.Move",
            (Func<object[], object>)(_ => mockMove.Object)).Execute();

        var macroCommand = Ioc.Resolve<ICommand>(
            "Commands.Macro",
            new object[] { new string[] { "Move", "", "  ", null! } }
        );

        macroCommand.Execute();

        mockMove.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void RegisterMacroCommand_WithCommandArray_ReturnsMacroCommand()
    {
        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();

        var macroCommand = Ioc.Resolve<ICommand>(
            "Commands.Macro",
            new object[] { new ICommand[] { mock1.Object, mock2.Object } }
        );

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mock1.Verify(c => c.Execute(), Times.Once);
        mock2.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void RegisterMacroCommand_WithSeparateCommandArgs_ReturnsMacroCommand()
    {
        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();

        var macroCommand = Ioc.Resolve<ICommand>(
            "Commands.Macro",
            mock1.Object,
            mock2.Object
        );

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mock1.Verify(c => c.Execute(), Times.Once);
        mock2.Verify(c => c.Execute(), Times.Once);
    }
}
