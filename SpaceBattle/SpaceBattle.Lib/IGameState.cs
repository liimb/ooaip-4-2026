using App;
using System;

namespace SpaceBattle.Lib;

public interface IGameState
{
    void Update();
    object GetState();
}
