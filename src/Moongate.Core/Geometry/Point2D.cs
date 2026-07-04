using System.Globalization;

namespace Moongate.Core.Geometry;

/// <summary>
/// An immutable 2D map coordinate. Distances are Chebyshev
/// (<c>max(|dx|, |dy|)</c>), matching how UO measures tile range.
/// </summary>
public readonly struct Point2D : IEquatable<Point2D>, IComparable<Point2D>, ISpanParsable<Point2D>
{
    public static readonly Point2D Zero = new(0, 0);

    public int X { get; }

    public int Y { get; }

    public Point2D(int x, int y)
    {
        X = x;
        Y = y;
    }

    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    /// <summary>Chebyshev distance in tiles: <c>max(|dx|, |dy|)</c>.</summary>
    public int DistanceTo(Point2D other)
    {
        return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
    }

    /// <summary>True when <paramref name="other"/> is within <paramref name="range"/> tiles.</summary>
    public bool InRange(Point2D other, int range)
    {
        return DistanceTo(other) <= range;
    }

    public bool Equals(Point2D other)
    {
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Point2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public int CompareTo(Point2D other)
    {
        var byX = X.CompareTo(other.X);

        return byX != 0 ? byX : Y.CompareTo(other.Y);
    }

    public static bool operator ==(Point2D left, Point2D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Point2D left, Point2D right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public static Point2D Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    public static Point2D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"'{s}' is not a valid Point2D; expected \"(x, y)\".");
        }

        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Point2D result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Point2D result)
    {
        result = default;
        s = s.Trim();

        if (s.Length < 5 || s[0] != '(' || s[^1] != ')')
        {
            return false;
        }

        var inner = s[1..^1];
        var comma = inner.IndexOf(',');

        if (comma < 0)
        {
            return false;
        }

        if (!int.TryParse(inner[..comma].Trim(), NumberStyles.Integer, provider, out var x) ||
            !int.TryParse(inner[(comma + 1)..].Trim(), NumberStyles.Integer, provider, out var y))
        {
            return false;
        }

        result = new Point2D(x, y);

        return true;
    }
}
