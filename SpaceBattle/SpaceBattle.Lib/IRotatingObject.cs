using App;

namespace SpaceBattle.Lib;

public interface IRotatingObject
{
    Angle Angle { get; set; }
    Angle AngularVelocity { get; }
}
