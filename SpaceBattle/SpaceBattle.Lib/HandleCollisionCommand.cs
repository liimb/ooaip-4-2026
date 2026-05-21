using App;
using System;

namespace SpaceBattle.Lib;

public class HandleCollisionCommand(object collision) : ICommand
{
    public void Execute()
    {
        Console.WriteLine($"Handling collision: {collision}");
    }
}
