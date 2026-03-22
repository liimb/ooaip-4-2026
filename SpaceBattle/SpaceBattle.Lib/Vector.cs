namespace SpaceBattle.Lib;

public class Vector
{
    private readonly int[] coordinates;

    public Vector(int[] coord)
    {
        coordinates = coord;
    }

    public int Dimension => coordinates.Length;

    public static Vector operator +(Vector a, Vector b)
    {
        if (a.Dimension != b.Dimension)
            throw new ArgumentException("Vectors must have the same dimension");

        var resultVector = a.coordinates.Zip(b.coordinates, (v1, v2) => v1 + v2).ToArray();

        return new Vector(resultVector);
    }

    public static bool operator ==(Vector a, Vector b)
    {
        if (ReferenceEquals(a, b))
            return true;

        return a.Equals(b);
    }

    public static bool operator !=(Vector a, Vector b)
    {
        return !(a == b);
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector other && coordinates.SequenceEqual(other.coordinates);
    }

    public override int GetHashCode()
    {
        return coordinates.Aggregate(0, (hash, value) => HashCode.Combine(hash, value));
    }
}
