namespace Moongate.Core.Geometry;

/// <summary>
/// An immutable axis-aligned tile volume. <see cref="Start"/> is inclusive,
/// <see cref="End"/> is exclusive on every axis.
/// </summary>
public readonly struct Rectangle3D : IEquatable<Rectangle3D>
{
    public Point3D Start { get; }

    public Point3D End { get; }

    public int Width
    {
        get { return End.X - Start.X; }
    }

    public int Height
    {
        get { return End.Y - Start.Y; }
    }

    public int Depth
    {
        get { return End.Z - Start.Z; }
    }

    public Rectangle3D(Point3D start, Point3D end)
    {
        Start = new Point3D(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Min(start.Z, end.Z));
        End = new Point3D(
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y),
            Math.Max(start.Z, end.Z));
    }

    public bool Contains(Point3D p)
    {
        return p.X >= Start.X && p.X < End.X &&
               p.Y >= Start.Y && p.Y < End.Y &&
               p.Z >= Start.Z && p.Z < End.Z;
    }

    public bool Equals(Rectangle3D other)
    {
        return Start == other.Start && End == other.End;
    }

    public override bool Equals(object? obj)
    {
        return obj is Rectangle3D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, End);
    }

    public static bool operator ==(Rectangle3D left, Rectangle3D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Rectangle3D left, Rectangle3D right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{Start}+({Width}, {Height}, {Depth})";
    }
}
