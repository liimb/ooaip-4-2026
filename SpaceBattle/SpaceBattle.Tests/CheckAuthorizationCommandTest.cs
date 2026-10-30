using System;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Tests;

public class CheckAuthorizationCommandTest
{
    public CheckAuthorizationCommandTest()
    {
        new InitCommand().Execute();
        var iocScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", iocScope).Execute();

        try
        {
            new RegisterIoCDependencyCheckAuthorizationCommand().Execute();
        }
        catch (ArgumentException)
        {
        }
    }

    [Fact]
    public void Should_AuthorizeWhenPathExists()
    {
        var tree = new PrefixTree<string, bool>();
        tree.Add(["Player1", "Ship1", "Shoot"], true);
        tree.Add(["Player1", "Ship1", "Move"], true);

        try
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Tree", (object[] args) => tree).Execute();
        }
        catch (ArgumentException)
        {
        }

        var authCmd = Ioc.Resolve<ICommand>("Authorization.Check", "Player1", "Ship1", "Shoot");

        Assert.Null(Record.Exception(authCmd.Execute));
    }

    [Fact]
    public void Should_ThrowWhenPathIsMissing()
    {
        var tree = new PrefixTree<string, bool>();
        tree.Add(["Player1", "Ship1", "Shoot"], true);
        tree.Add(["Player1", "Ship2", "Shoot"], true);
        tree.Add(["Player1", "Ship999", "Move"], true);

        try
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Tree", (object[] args) => tree).Execute();
        }
        catch (ArgumentException)
        {
        }

        var authCmd = Ioc.Resolve<ICommand>("Authorization.Check", "Player1", "Ship999", "Shoot");

        Assert.Throws<UnauthorizedAccessException>(authCmd.Execute);
    }

    [Fact]
    public void AShould_ThrowWhenTreeIsEmpty()
    {
        var tree = new PrefixTree<string, bool>();

        try
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Authorization.Tree", (object[] args) => tree).Execute();
        }
        catch (ArgumentException)
        {
        }

        var authCmd = Ioc.Resolve<ICommand>("Authorization.Check", "Player1", "Ship1", "Shoot");

        Assert.Throws<UnauthorizedAccessException>(authCmd.Execute);
    }
}