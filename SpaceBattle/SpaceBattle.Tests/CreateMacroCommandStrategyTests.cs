using App;
using App.Scopes;
using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class CreateMacroCommandStrategyTests
{
    public CreateMacroCommandStrategyTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void Resolve_WithValidSpec_ReturnsMacroCommand()
    {
        var specName = "ValidSpec_" + Guid.NewGuid().ToString("N");
        var commandNames = new[] { "CommandA", "CommandB" };

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Specs." + specName,
            (object[] args) => commandNames
        ).Execute();

        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.CommandA",
            (object[] args) => mock1.Object
        ).Execute();

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Commands.CommandB",
            (object[] args) => mock2.Object
        ).Execute();

        var strategy = new CreateMacroCommandStrategy(specName);

        var macroCommand = strategy.Resolve(new object[] { });

        Assert.NotNull(macroCommand);
        Assert.IsType<MacroCommand>(macroCommand);

        macroCommand.Execute();

        mock1.Verify(x => x.Execute(), Times.Once);
        mock2.Verify(x => x.Execute(), Times.Once);
    }

    [Fact]
    public void Resolve_WhenSpecNotFound_ThrowsException()
    {
        var strategy = new CreateMacroCommandStrategy("NonExistentSpec_" + Guid.NewGuid().ToString("N"));

        Assert.Throws<Exception>(() => strategy.Resolve(new object[] { }));
    }

    [Fact]
    public void Resolve_WhenSpecReturnsEmptyArray_ThrowsException()
    {
        var specName = "EmptySpec_" + Guid.NewGuid().ToString("N");

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Specs." + specName,
            (object[] args) => new string[0]
        ).Execute();

        var strategy = new CreateMacroCommandStrategy(specName);

        var exception = Assert.Throws<InvalidOperationException>(() => strategy.Resolve(new object[] { }));
        Assert.Contains("No commands found", exception.Message);
    }

    [Fact]
    public void Constructor_WithNullSpec_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateMacroCommandStrategy(null!));
    }

    [Fact]
    public void Resolve_WhenCommandNamesNull_ThrowsException()
    {
        var specName = "NullSpec_" + Guid.NewGuid().ToString("N");

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Specs." + specName,
            (object[] args) => (string[]?)null
        ).Execute();

        var strategy = new CreateMacroCommandStrategy(specName);

        var exception = Assert.Throws<InvalidOperationException>(() => strategy.Resolve(new object[] { }));
        Assert.Contains("No commands found", exception.Message);
    }

    [Fact]
    public void Resolve_WhenCommandNotFound_ThrowsException()
    {
        var specName = "NotFoundSpec_" + Guid.NewGuid().ToString("N");
        var commandNames = new[] { "NonExistentCommand_" + Guid.NewGuid().ToString("N") };

        Ioc.Resolve<ICommand>(
            "IoC.Register",
            "Specs." + specName,
            (object[] args) => commandNames
        ).Execute();

        var strategy = new CreateMacroCommandStrategy(specName);

        Assert.Throws<Exception>(() => strategy.Resolve(new object[] { }));
    }
}
