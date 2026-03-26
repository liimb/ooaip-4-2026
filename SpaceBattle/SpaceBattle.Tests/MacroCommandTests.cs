using Moq;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class MacroCommandTests
{
    [Fact]
    public void MacroCommand_ExecutesAllCommands()
    {
        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();
        var mock3 = new Mock<ICommand>();

        var macroCommand = new MacroCommand(mock1.Object, mock2.Object, mock3.Object);

        macroCommand.Execute();

        mock1.Verify(x => x.Execute(), Times.Once);
        mock2.Verify(x => x.Execute(), Times.Once);
        mock3.Verify(x => x.Execute(), Times.Once);
    }

    [Fact]
    public void MacroCommand_WhenCommandThrowsException_StopsExecution()
    {
        var mock1 = new Mock<ICommand>();
        var mock2 = new Mock<ICommand>();
        var mock3 = new Mock<ICommand>();

        mock1.Setup(x => x.Execute()).Throws<InvalidOperationException>();

        var macroCommand = new MacroCommand(mock1.Object, mock2.Object, mock3.Object);

        Assert.Throws<InvalidOperationException>(() => macroCommand.Execute());

        mock1.Verify(x => x.Execute(), Times.Once);
        mock2.Verify(x => x.Execute(), Times.Never);
        mock3.Verify(x => x.Execute(), Times.Never);
    }

    [Fact]
    public void MacroCommand_WithEmptyArray_DoesNothing()
    {
        var macroCommand = new MacroCommand();
        macroCommand.Execute();
        Assert.True(true);
    }

    [Fact]
    public void MacroCommand_ConstructorWithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MacroCommand(null!));
    }
}
