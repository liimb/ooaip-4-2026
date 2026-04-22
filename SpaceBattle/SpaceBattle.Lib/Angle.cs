namespace SpaceBattle.Lib;

public class Angle
{
    private static int _denominator = 8;
    private readonly int _numerator;

    public Angle(int numerator)
    {
        _numerator = numerator % _denominator;
        if (_numerator < 0) _numerator += _denominator;
    }

    public static Angle operator +(Angle a, Angle b)
    {
        return new Angle(a._numerator + b._numerator);
    }

    public static bool operator ==(Angle a, Angle b)
    {
        return a._numerator == b._numerator;
    }

    public static bool operator !=(Angle a, Angle b) => !(a == b);

    public override bool Equals(object? obj)
    {
        if (obj is Angle other)
            return _numerator == other._numerator;
        return false;
    }

    public override int GetHashCode()
    {
        return _numerator.GetHashCode();
    }

    public double ToRadians()
    {
        return (_numerator * 2 * Math.PI) / _denominator;
    }

    public static implicit operator double(Angle angle)
    {
        return angle.ToRadians();
    }

    public override string ToString()
    {
        return $"{_numerator}/{_denominator}π";
    }
}
