using System.Runtime.CompilerServices;

namespace Moongate.Core.Geometry;

/// <summary>
/// Represents Rectangle2D.
/// </summary>
public struct Rectangle2D : IEquatable<Rectangle2D>, ISpanFormattable, ISpanParsable<Rectangle2D>
{
    private Point2D _start;
    private Point2D _end;

    public static Rectangle2D Empty => new();

    public Point2D Start
    {
        get => _start;
        set => _start = value;
    }

    public Point2D End
    {
        get => _end;
        set => _end = value;
    }

    public int X
    {
        get => _start.X;
        set => _start.X = value;
    }

    public int Y
    {
        get => _start.Y;
        set => _start.Y = value;
    }

    public int Width
    {
        get => _end.X - _start.X;
        set => _end.X = _start.X + value;
    }

    public int Height
    {
        get => _end.Y - _start.Y;
        set => _end.Y = _start.Y + value;
    }

    public Rectangle2D(Point2D start, Point2D end)
    {
        _start = start;
        _end = end;
    }

    public Rectangle2D(int x, int y, int width, int height)
    {
        _start = new(x, y);
        _end = new(x + width, y + height);
    }

    public bool Contains(Point3D p)
    {
        return _start.X <= p.X && _start.Y <= p.Y && _end.X > p.X && _end.Y > p.Y;
    }

    public bool Contains(Point2D p)
    {
        return _start.X <= p.X && _start.Y <= p.Y && _end.X > p.X && _end.Y > p.Y;
    }

    public bool Contains(int x, int y)
    {
        return _start.X <= x && _start.Y <= y && _end.X > x && _end.Y > y;
    }

    public bool Equals(Rectangle2D other)
    {
        return _start == other._start && _end == other._end;
    }

    public override bool Equals(object? obj)
    {
        return obj is Rectangle2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_start, _end);
    }

    public void MakeHold(Rectangle2D r)
    {
        if (r._start.X < _start.X)
        {
            _start.X = r._start.X;
        }

        if (r._start.Y < _start.Y)
        {
            _start.Y = r._start.Y;
        }

        if (r._end.X > _end.X)
        {
            _end.X = r._end.X;
        }

        if (r._end.Y > _end.Y)
        {
            _end.Y = r._end.Y;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle2D Parse(string s)
    {
        return Parse(s, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle2D Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), provider);
    }

    public static Rectangle2D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        return result;
    }

    public void Set(int x, int y, int width, int height)
    {
        _start = new(x, y);
        _end = new(x + width, y + height);
    }

    public override string ToString()
    {
        return ToString(null, null);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return string.Create(formatProvider, $"({X}, {Y})+({Width}, {Height})");
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return destination.TryWrite(provider, $"({X}, {Y})+({Width}, {Height})", out charsWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? s, IFormatProvider? provider, out Rectangle2D result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Rectangle2D result)
    {
        result = default;
        s = s.Trim();
        var startEnd = s.IndexOf(')');

        if (startEnd < 0 || !Point2D.TryParse(s[..(startEnd + 1)], provider, out var start))
        {
            return false;
        }

        var remainder = s[(startEnd + 1)..].TrimStart();

        if (remainder.IsEmpty ||
            remainder[0] != '+' ||
            !Point2D.TryParse(remainder[1..], provider, out var size))
        {
            return false;
        }

        result = new(start.X, start.Y, size.X, size.Y);

        return true;
    }

    public static bool operator ==(Rectangle2D l, Rectangle2D r)
        => l._start == r._start && l._end == r._end;

    public static bool operator !=(Rectangle2D l, Rectangle2D r)
        => l._start != r._start || l._end != r._end;
}
