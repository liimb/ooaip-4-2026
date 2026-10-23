using SpaceBattle.Lib;
using App;
using App.Scopes;

namespace SpaceBattle.Tests;

public class RegisterIoCDependencyGameCommandTests
{
    public RegisterIoCDependencyGameCommandTests()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();
    }

    [Fact]
    public void Execute_ShouldRegisterDependenciesWithoutErrors()
    {
        var registerCommand = new RegisterIoCDependencyGameCommand();

        var exception = Record.Exception(() => registerCommand.Execute());

        Assert.Null(exception);
    }
}
