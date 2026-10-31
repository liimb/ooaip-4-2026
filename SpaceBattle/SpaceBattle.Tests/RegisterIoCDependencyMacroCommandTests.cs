using System.Linq;
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

        var newScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", newScope).Execute();

        new RegisterIoCDependencyMacroCommand().Execute();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void RegisterMacroCommand_WithMultipleCommands_DependencyResolves(int count)
    {

        var commands = Enumerable.Range(0, count)
            .Select(_ => new Mock<ICommand>().Object)
            .ToArray();

        var macroCommand = Ioc.Resolve<ICommand>("Commands.Macro", commands);

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);
    }

    [Fact]
    public void RegisterMacroCommand_WithArrayOfCommands_DependencyResolves()
    {

        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();
        var commandsArray = new ICommand[] { mock1.Object, mock2.Object };

        var macroCommand = Ioc.Resolve<ICommand>("Commands.Macro", new object[] { commandsArray });

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);
    }

    [Fact]
    public void RegisterMacroCommand_WithNullArgs_DependencyResolves()
    {

        var macroCommand = Ioc.Resolve<ICommand>("Commands.Macro", (object[])null!);

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);
    }
}
