using System.Globalization;
using System.Runtime.CompilerServices;
using Moongate.Core.Interfaces.Geometry;

namespace Moongate.Core.Geometry;

/// <summary>
///     Represents Point2D.
/// </summary>
public struct Point2D
    : IPoint2D, IComparable<Point2D>, IComparable<IPoint2D>, IEquatable<object>, IEquatable<Point2D>,
      IEquatable<IPoint2D>, ISpanFormattable, ISpanParsable<Point2D>
{
    public static readonly Point2D Zero = new(0, 0);

    public int X { get; set; }

    public int Y { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point2D(IPoint2D p) : this(p.X, p.Y) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point2D(Point3D p) : this(p.X, p.Y) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point2D(Point2D p) : this(p.X, p.Y) { }

    public Point2D(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int CompareTo(Point2D other)
    {
        var xComparison = X.CompareTo(other.X);

        return xComparison != 0 ? xComparison : Y.CompareTo(other.Y);
    }

    public int CompareTo(IPoint2D? other)
    {
        if (other is null)
        {
            return 1;
        }

        var xComparison = X.CompareTo(other.X);

        return xComparison != 0 ? xComparison : Y.CompareTo(other.Y);
    }

    public bool Equals(Point2D other)
    {
        return X == other.X && Y == other.Y;
    }

    public bool Equals(IPoint2D? other)
    {
        return X == other?.X && Y == other.Y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Point2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Point2D l, Point2D r)
    {
        return l.X == r.X && l.Y == r.Y;
    }

    public static bool operator ==(Point2D l, IPoint2D? r)
    {
        return !ReferenceEquals(r, null) && l.X == r.X && l.Y == r.Y;
    }

    public static bool operator >(Point2D l, Point2D r)
    {
        return l.X > r.X && l.Y > r.Y;
    }

    public static bool operator >(Point2D l, IPoint2D? r)
    {
        return !ReferenceEquals(r, null) && l.X > r.X && l.Y > r.Y;
    }

    public static bool operator >=(Point2D l, Point2D r)
    {
        return l.X >= r.X && l.Y >= r.Y;
    }

    public static bool operator >=(Point2D l, IPoint2D? r)
    {
        return !ReferenceEquals(r, null) && l.X >= r.X && l.Y >= r.Y;
    }

    public static bool operator !=(Point2D l, Point2D r)
    {
        return l.X != r.X || l.Y != r.Y;
    }

    public static bool operator !=(Point2D l, IPoint2D? r)
    {
        return r is null || l.X != r.X || l.Y != r.Y;
    }

    public static bool operator <(Point2D l, Point2D r)
    {
        return l.X < r.X && l.Y < r.Y;
    }

    public static bool operator <(Point2D l, IPoint2D? r)
    {
        return !ReferenceEquals(r, null) && l.X < r.X && l.Y < r.Y;
    }

    public static bool operator <=(Point2D l, Point2D r)
    {
        return l.X <= r.X && l.Y <= r.Y;
    }

    public static bool operator <=(Point2D l, IPoint2D? r)
    {
        return !ReferenceEquals(r, null) && l.X <= r.X && l.Y <= r.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point2D Parse(string s)
    {
        return Parse(s, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point2D Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    public static Point2D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        s = s.Trim();

        if (s.IsEmpty || s[0] != '(' || s[^1] != ')')
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var comma = s.IndexOf(',');

        if (comma == -1)
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var first = s.Slice(1, comma - 1).Trim();

        if (!int.TryParse(first, NumberStyles.Integer, provider, out var x))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var second = s.Slice(comma + 1, s.Length - comma - 2).Trim();

        if (!int.TryParse(second, NumberStyles.Integer, provider, out var y))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        return new Point2D(x, y);
    }

    public override string ToString()
    {
        return ToString(null, null);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return string.Create(formatProvider, $"({X}, {Y})");
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return destination.TryWrite(provider, $"({X}, {Y})", out charsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? s, IFormatProvider? provider, out Point2D result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Point2D result)
    {
        s = s.Trim();

        if (s.IsEmpty || s[0] != '(' || s[^1] != ')')
        {
            result = default;

            return false;
        }

        var comma = s.IndexOf(',');

        if (comma == -1)
        {
            result = default;

            return false;
        }

        var first = s.Slice(1, comma - 1).Trim();

        if (!int.TryParse(first, NumberStyles.Integer, provider, out var x))
        {
            result = default;

            return false;
        }

        var second = s.Slice(comma + 1, s.Length - comma - 2).Trim();

        if (!int.TryParse(second, NumberStyles.Integer, provider, out var y))
        {
            result = default;

            return false;
        }

        result = new Point2D(x, y);

        return true;
    }
}
