using App;

namespace SpaceBattle.Lib;

public class RotateCommand : ICommand
{
    private readonly IRotatingObject _rotatingObject;

    public RotateCommand(IRotatingObject rotatingObject)
    {
        _rotatingObject = rotatingObject;
    }

    public void Execute()
    {
        var currentAngle = _rotatingObject.Angle;
        var angularVelocity = _rotatingObject.AngularVelocity;
        var newAngle = currentAngle + angularVelocity;
        _rotatingObject.Angle = newAngle;
    }
}
