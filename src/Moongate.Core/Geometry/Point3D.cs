using System.Globalization;

namespace Moongate.Core.Geometry;

/// <summary>
/// An immutable 3D map coordinate. Distance and range checks are 2D Chebyshev
/// (Z is ignored), matching UO tile-range semantics.
/// </summary>
public readonly struct Point3D : IEquatable<Point3D>, IComparable<Point3D>, ISpanParsable<Point3D>
{
    public static readonly Point3D Zero = new(0, 0, 0);

    public int X { get; }

    public int Y { get; }

    public int Z { get; }

    public Point3D(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Point3D(Point2D p, int z)
    {
        X = p.X;
        Y = p.Y;
        Z = z;
    }

    public int CompareTo(Point3D other)
    {
        var byX = X.CompareTo(other.X);

        if (byX != 0)
        {
            return byX;
        }

        var byY = Y.CompareTo(other.Y);

        return byY != 0 ? byY : Z.CompareTo(other.Z);
    }

    public void Deconstruct(out int x, out int y, out int z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    /// <summary>2D Chebyshev distance in tiles; Z is ignored.</summary>
    public int DistanceTo(Point3D other)
        => ToPoint2D().DistanceTo(other.ToPoint2D());

    public bool Equals(Point3D other)
        => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj)
        => obj is Point3D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);

    /// <summary>True when <paramref name="other" /> is within <paramref name="range" /> tiles (Z ignored).</summary>
    public bool InRange(Point3D other, int range)
        => DistanceTo(other) <= range;

    public static bool operator ==(Point3D left, Point3D right)
        => left.Equals(right);

    public static implicit operator Point2D(Point3D p)
        => new(p.X, p.Y);

    public static bool operator !=(Point3D left, Point3D right)
        => !left.Equals(right);

    public static Point3D Parse(string s, IFormatProvider? provider)
        => Parse(s.AsSpan(), provider);

    public static Point3D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"'{s}' is not a valid Point3D; expected \"(x, y, z)\".");
        }

        return result;
    }

    public Point2D ToPoint2D()
        => new(X, Y);

    public override string ToString()
        => $"({X}, {Y}, {Z})";

    public static bool TryParse(string? s, IFormatProvider? provider, out Point3D result)
        => TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Point3D result)
    {
        result = default;
        s = s.Trim();

        if (s.Length < 7 || s[0] != '(' || s[^1] != ')')
        {
            return false;
        }

        var inner = s[1..^1];
        var firstComma = inner.IndexOf(',');

        if (firstComma < 0)
        {
            return false;
        }

        var rest = inner[(firstComma + 1)..];
        var secondComma = rest.IndexOf(',');

        if (secondComma < 0 || rest[(secondComma + 1)..].IndexOf(',') >= 0)
        {
            return false;
        }

        if (!int.TryParse(inner[..firstComma].Trim(), NumberStyles.Integer, provider, out var x) ||
            !int.TryParse(rest[..secondComma].Trim(), NumberStyles.Integer, provider, out var y) ||
            !int.TryParse(rest[(secondComma + 1)..].Trim(), NumberStyles.Integer, provider, out var z))
        {
            return false;
        }

        result = new(x, y, z);

        return true;
    }
}
