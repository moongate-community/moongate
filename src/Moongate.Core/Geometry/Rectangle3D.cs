using System.Runtime.CompilerServices;
using Moongate.Core.Interfaces.Geometry;

namespace Moongate.Core.Geometry;

/// <summary>
/// Represents Rectangle3D.
/// </summary>
public struct Rectangle3D : IEquatable<Rectangle3D>, ISpanFormattable
{
    private Point3D _start;
    private Point3D _end;

    public Point3D Start
    {
        get => _start;
        set => _start = value;
    }

    public Point3D End
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

    public int Z
    {
        get => _start.Z;
        set => _start.Z = value;
    }

    public int Width => _end.X - _start.X;

    public int Height => _end.Y - _start.Y;

    public int Depth => _end.Z - _start.Z;

    public Rectangle3D(Point3D start, Point3D end)
    {
        _start = start;
        _end = end;
    }

    public Rectangle3D(int x, int y, int z, int width, int height, int depth)
    {
        _start = new(x, y, z);
        _end = new(x + width, y + height, z + depth);
    }

    public bool Contains(Point3D p)
        => p.X >= _start.X && p.X < _end.X && p.Y >= _start.Y && p.Y < _end.Y && p.Z >= _start.Z && p.Z < _end.Z;

    public bool Contains(Point2D p)
        => p.X >= _start.X && p.X < _end.X && p.Y >= _start.Y && p.Y < _end.Y;

    public bool Contains(IPoint2D p)
        => p.X >= _start.X && p.X < _end.X && p.Y >= _start.Y && p.Y < _end.Y;

    public bool Contains(IPoint3D p)
        => p.X >= _start.X && p.X < _end.X && p.Y >= _start.Y && p.Y < _end.Y && p.Z >= _start.Z && p.Z < _end.Z;

    public bool Equals(Rectangle3D other)
        => _start == other._start && _end == other._end;

    public override bool Equals(object? obj)
        => obj is Rectangle3D other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(_start, _end);

    public void MakeHold(Rectangle3D r)
    {
        if (r._start.X < _start.X)
        {
            _start.X = r._start.X;
        }

        if (r._start.Y < _start.Y)
        {
            _start.Y = r._start.Y;
        }

        if (r._start.Z < _start.Z)
        {
            _start.Z = r._start.Z;
        }

        if (r._end.X > _end.X)
        {
            _end.X = r._end.X;
        }

        if (r._end.Y > _end.Y)
        {
            _end.Y = r._end.Y;
        }

        if (r._end.Z > _end.Z)
        {
            _end.Z = r._end.Z;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle3D Parse(string s)
        => Parse(s, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle3D Parse(string s, IFormatProvider? provider)
        => Parse(s.AsSpan(), provider);

    public static Rectangle3D Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        return result;
    }

    public override string ToString()
        => ToString(null, null);

    public string ToString(string? format, IFormatProvider? formatProvider)
        => string.Create(formatProvider, $"({X}, {Y}, {Z})+({Width}, {Height}, {Depth})");

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => destination.TryWrite(provider, $"({X}, {Y}, {Z})+({Width}, {Height}, {Depth})", out charsWritten);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? s, IFormatProvider? provider, out Rectangle3D result)
        => TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Rectangle3D result)
    {
        result = default;
        s = s.Trim();
        var startEnd = s.IndexOf(')');

        if (startEnd < 0 || !Point3D.TryParse(s[..(startEnd + 1)], provider, out var start))
        {
            return false;
        }

        var remainder = s[(startEnd + 1)..].TrimStart();

        if (remainder.IsEmpty ||
            remainder[0] != '+' ||
            !Point3D.TryParse(remainder[1..], provider, out var size))
        {
            return false;
        }

        result = new(start.X, start.Y, start.Z, size.X, size.Y, size.Z);

        return true;
    }

    public static bool operator ==(Rectangle3D l, Rectangle3D r)
        => l._start == r._start && l._end == r._end;

    public static bool operator !=(Rectangle3D l, Rectangle3D r)
        => l._start != r._start || l._end != r._end;
}
