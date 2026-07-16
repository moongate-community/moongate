namespace Moongate.Core.Geometry;

/// <summary>
/// An immutable axis-aligned tile volume. <see cref="Start" /> is inclusive,
/// <see cref="End" /> is exclusive on every axis.
/// </summary>
public readonly struct Rectangle3D : IEquatable<Rectangle3D>
{
    public Point3D Start { get; }

    public Point3D End { get; }

    public int Width => End.X - Start.X;

    public int Height => End.Y - Start.Y;

    public int Depth => End.Z - Start.Z;

    public Rectangle3D(Point3D start, Point3D end)
    {
        Start = new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Min(start.Z, end.Z)
        );
        End = new(
            Math.Max(start.X, end.X),
            Math.Max(start.Y, end.Y),
            Math.Max(start.Z, end.Z)
        );
    }

    public bool Contains(Point3D p)
        => p.X >= Start.X &&
           p.X < End.X &&
           p.Y >= Start.Y &&
           p.Y < End.Y &&
           p.Z >= Start.Z &&
           p.Z < End.Z;

    public bool Equals(Rectangle3D other)
        => Start == other.Start && End == other.End;

    public override bool Equals(object? obj)
        => obj is Rectangle3D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Start, End);

    public static bool operator ==(Rectangle3D left, Rectangle3D right)
        => left.Equals(right);

    public static bool operator !=(Rectangle3D left, Rectangle3D right)
        => !left.Equals(right);

    public override string ToString()
        => $"{Start}+({Width}, {Height}, {Depth})";
}
