using System;
using App;
using System.Collections.Generic;

namespace SpaceBattle.Lib;

public class CollisionTree
{
    private readonly HashSet<(int x, int y, int vx, int vy)> _collisionStates;

    public CollisionTree(IEnumerable<(int x, int y, int vx, int vy)> collisionData)
    {
        _collisionStates = new HashSet<(int x, int y, int vx, int vy)>(collisionData);
    }

    public bool Contains(int[] state)
    {
        if (state.Length != 4) return false;
        return _collisionStates.Contains((state[0], state[1], state[2], state[3]));
    }

    public bool Contains((int x, int y, int vx, int vy) state)
    {
        return _collisionStates.Contains(state);
    }
}
