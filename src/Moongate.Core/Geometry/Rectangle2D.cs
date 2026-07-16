namespace Moongate.Core.Geometry;

/// <summary>
/// An immutable axis-aligned tile rectangle. <see cref="Start" /> is inclusive,
/// <see cref="End" /> is exclusive.
/// </summary>
public readonly struct Rectangle2D : IEquatable<Rectangle2D>
{
    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public Point2D Start => new(X, Y);

    public Point2D End => new(X + Width, Y + Height);

    public Rectangle2D(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rectangle2D(Point2D start, Point2D end)
    {
        X = Math.Min(start.X, end.X);
        Y = Math.Min(start.Y, end.Y);
        Width = Math.Abs(end.X - start.X);
        Height = Math.Abs(end.Y - start.Y);
    }

    public bool Contains(Point2D p)
        => p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;

    public bool Contains(Point3D p)
        => Contains(p.ToPoint2D());

    public bool Equals(Rectangle2D other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj)
        => obj is Rectangle2D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(Rectangle2D left, Rectangle2D right)
        => left.Equals(right);

    public static bool operator !=(Rectangle2D left, Rectangle2D right)
        => !left.Equals(right);

    public override string ToString()
        => $"({X}, {Y})+({Width}, {Height})";
}
