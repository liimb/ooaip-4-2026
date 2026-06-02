using System;
using App;

namespace SpaceBattle.Lib;

public interface ICollisionObject
{
    string Form { get; }
    IVector Position { get; }
    IVector Velocity { get; }
}
