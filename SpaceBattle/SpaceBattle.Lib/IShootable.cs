using System;

namespace SpaceBattle.Lib;

public interface IShootable
{
    Vector Position { get; }
    Angle Angle { get; }
}
