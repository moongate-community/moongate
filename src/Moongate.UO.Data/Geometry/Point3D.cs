/*************************************************************************
 * ModernUO                                                              *
 * Copyright 2019-2023 - ModernUO Development Team                       *
 * Email: hi@modernuo.com                                                *
 * File: Point3D.cs                                                      *
 *                                                                       *
 * This program is free software: you can redistribute it and/or modify  *
 * it under the terms of the GNU General Public License as published by  *
 * the Free Software Foundation, either version 3 of the License, or     *
 * (at your option) any later version.                                   *
 *                                                                       *
 * You should have received a copy of the GNU General Public License     *
 * along with this program.  If not, see <http://www.gnu.org/licenses/>. *
 *************************************************************************/

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Moongate.Core.Extensions.Strings;
using Moongate.UO.Data.Interfaces.Geometry;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Geometry;

public struct Point3D
    : IPoint3D, IComparable<Point3D>, IComparable<IPoint3D>, IEquatable<object>, IEquatable<Point3D>,
        IEquatable<IPoint3D>, ISpanFormattable, ISpanParsable<Point3D>, INotifyPropertyChanged
{
#pragma warning disable 67
    public event PropertyChangedEventHandler? PropertyChanged;

#pragma warning restore 67

    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public static readonly Point3D Zero = new(0, 0, 0);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point3D(IPoint3D p) : this(p.X, p.Y, p.Z)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point3D(Point3D p) : this(p.X, p.Y, p.Z)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Point3D(Point2D p, int z) : this(p.X, p.Y, z)
    {
    }

    public Point3D(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(Point3D other) => X == other.X && Y == other.Y && Z == other.Z;

    public bool Equals(IPoint3D other) =>
        X == other?.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object obj) => obj is Point3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Point3D l, Point3D r) => l.X == r.X && l.Y == r.Y && l.Z == r.Z;

    public static bool operator ==(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && l.X == r.X && l.Y == r.Y && l.Z == r.Z;

    public static bool operator !=(Point3D l, Point3D r) => l.X != r.X || l.Y != r.Y || l.Z != r.Z;

    public static bool operator !=(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && (l.X != r.X || l.Y != r.Y || l.Z != r.Z);

    public static bool operator >(Point3D l, Point3D r) => l.X > r.X && l.Y > r.Y && l.Z > r.Z;

    public static bool operator >(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && l.X > r.X && l.Y > r.Y && l.Z > r.Z;

    public static bool operator <(Point3D l, Point3D r) => l.X < r.X && l.Y < r.Y && l.Z > r.Z;

    public static bool operator <(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && l.X < r.X && l.Y < r.Y && l.Z > r.Z;

    public static bool operator >=(Point3D l, Point3D r) => l.X >= r.X && l.Y >= r.Y && l.Z > r.Z;

    public static bool operator >=(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && l.X >= r.X && l.Y >= r.Y && l.Z > r.Z;

    public static bool operator <=(Point3D l, Point3D r) => l.X <= r.X && l.Y <= r.Y && l.Z > r.Z;

    public static bool operator <=(Point3D l, IPoint3D r) =>
        !ReferenceEquals(r, null) && l.X <= r.X && l.Y <= r.Y && l.Z > r.Z;

    public int CompareTo(Point3D other)
    {
        var xComparison = X.CompareTo(other.X);
        if (xComparison != 0)
        {
            return xComparison;
        }

        var yComparison = Y.CompareTo(other.Y);
        if (yComparison != 0)
        {
            return yComparison;
        }

        return Z.CompareTo(other.Z);
    }

    public int CompareTo(IPoint3D other)
    {
        var xComparison = X.CompareTo(other.X);
        if (xComparison != 0)
        {
            return xComparison;
        }

        var yComparison = Y.CompareTo(other.Y);
        if (yComparison != 0)
        {
            return yComparison;
        }

        return Z.CompareTo(other.Z);
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider)
        => destination.TryWrite(provider, $"({X}, {Y}, {Z})", out charsWritten);

    public override string ToString()
    {
        // Maximum number of characters that are needed to represent this:
        // 6 characters for (, , )
        // Up to 11 characters to represent each integer
        const int maxLength = 6 + 11 * 3;
        Span<char> span = stackalloc char[maxLength];
        TryFormat(span, out var charsWritten, null, null);
        return span[..charsWritten].ToString();
    }

    public string ToString(string format, IFormatProvider formatProvider)
    {
        // format and formatProvider are not doing anything right now, so use the
        // default ToString implementation.
        return ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point3D Parse(string s) => Parse(s, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point3D Parse(string s, IFormatProvider provider) => Parse(s.AsSpan(), provider);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string s, IFormatProvider provider, out Point3D result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static Point3D Parse(ReadOnlySpan<char> s, IFormatProvider provider)
    {
        s = s.Trim();

        if (!s.StartsWithOrdinal('(') || !s.EndsWithOrdinal(')'))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var firstComma = s.IndexOfOrdinal(',');
        if (firstComma == -1)
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var first = s.Slice(1, firstComma - 1).Trim();

        if (!int.TryParse(first, out var x))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var offset = firstComma + 1;

        var secondComma = s[offset..].IndexOfOrdinal(',');
        if (secondComma == -1)
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        var second = s.Slice(firstComma + 1, secondComma).Trim();

        if (!int.TryParse(second, out var y))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        offset += secondComma + 1;

        var third = s.Slice(offset, s.Length - offset - 1).Trim();
        if (!int.TryParse(third, out var z))
        {
            throw new FormatException($"The input string '{s}' was not in a correct format.");
        }

        return new Point3D(x, y, z);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out Point3D result)
    {
        s = s.Trim();

        if (!s.StartsWithOrdinal('(') || !s.EndsWithOrdinal(')'))
        {
            result = default;
            return false;
        }

        var firstComma = s.IndexOfOrdinal(',');
        if (firstComma == -1)
        {
            result = default;
            return false;
        }

        var first = s.Slice(1, firstComma - 1).Trim();
        if (!int.TryParse(first, out var x))
        {
            result = default;
            return false;
        }

        var offset = firstComma + 1;

        var secondComma = s[offset..].IndexOfOrdinal(',');
        if (secondComma == -1)
        {
            result = default;
            return false;
        }

        var second = s.Slice(firstComma + 1, secondComma).Trim();
        if (!int.TryParse(second, out var y))
        {
            result = default;
            return false;
        }

        offset += secondComma + 1;

        var third = s.Slice(offset, s.Length - offset - 1).Trim();
        if (!int.TryParse(third, out var z))
        {
            result = default;
            return false;
        }

        result = new Point3D(x, y, z);
        return true;
    }

    /// <summary>
    /// Implicit conversion from DirectionType to Point3D offset
    /// Converts direction to movement offset coordinates
    /// </summary>
    /// <param name="direction">Direction to convert</param>
    /// <returns>Point3D with offset coordinates</returns>
    public static implicit operator Point3D(DirectionType direction)
    {
        // Remove running flag to get base direction
        var baseDirection = (DirectionType)((byte)direction & (byte)DirectionType.Mask);

        return baseDirection switch
        {
            DirectionType.North => new Point3D(0, -1, 0),  // North: Y decreases
            DirectionType.Right => new Point3D(1, -1, 0),  // Northeast: X+, Y-
            DirectionType.East  => new Point3D(1, 0, 0),   // East: X increases
            DirectionType.Down  => new Point3D(1, 1, 0),   // Southeast: X+, Y+
            DirectionType.South => new Point3D(0, 1, 0),   // South: Y increases
            DirectionType.Left  => new Point3D(-1, 1, 0),  // Southwest: X-, Y+
            DirectionType.West  => new Point3D(-1, 0, 0),  // West: X decreases
            DirectionType.Up    => new Point3D(-1, -1, 0), // Northwest: X-, Y-
            _                   => new Point3D(0, 0, 0)    // No movement
        };
    }

    /// <summary>
    /// Implicit conversion from Point3D to DirectionType
    /// Converts movement offset to direction (ignores Z coordinate)
    /// </summary>
    /// <param name="point">Point3D offset to convert</param>
    /// <returns>DirectionType representing the movement direction</returns>
    public static implicit operator DirectionType(Point3D point)
    {
        // Normalize the coordinates to -1, 0, or 1
        int deltaX = Math.Sign(point.X);
        int deltaY = Math.Sign(point.Y);

        return (deltaX, deltaY) switch
        {
            (0, -1)  => DirectionType.North, // North: Y-
            (1, -1)  => DirectionType.Right, // Northeast: X+, Y-
            (1, 0)   => DirectionType.East,  // East: X+
            (1, 1)   => DirectionType.Down,  // Southeast: X+, Y+
            (0, 1)   => DirectionType.South, // South: Y+
            (-1, 1)  => DirectionType.Left,  // Southwest: X-, Y+
            (-1, 0)  => DirectionType.West,  // West: X-
            (-1, -1) => DirectionType.Up,    // Northwest: X-, Y-
            _        => DirectionType.North  // Default to North for no movement
        };
    }

    /// <summary>
    /// Adds a direction offset to current position
    /// </summary>
    /// <param name="direction">Direction to move</param>
    /// <returns>New Point3D with offset applied</returns>
    public Point3D Move(DirectionType direction)
    {
        Point3D offset = direction; // Uses implicit c fonversion
        return new Point3D(X + offset.X, Y + offset.Y, Z + offset.Z);
    }

    /// <summary>
    /// Gets direction from current point to target point
    /// </summary>
    /// <param name="target">Target point</param>
    /// <returns>Direction to target</returns>
    public DirectionType GetDirectionTo(Point3D target)
    {
        Point3D delta = new Point3D(target.X - X, target.Y - Y, target.Z - Z);
        return delta; // Uses implicit conversion
    }

    /// <summary>
    /// Checks if a direction includes running flag
    /// </summary>
    /// <param name="direction">Direction to check</param>
    /// <returns>True if running flag is set</returns>
    public static bool IsRunning(DirectionType direction)
    {
        return ((byte)direction & (byte)DirectionType.Running) != 0;
    }

    /// <summary>
    /// Adds running flag to direction
    /// </summary>
    /// <param name="direction">Base direction</param>
    /// <returns>Direction with running flag</returns>
    public static DirectionType SetRunning(DirectionType direction)
    {
        return (DirectionType)((byte)direction | (byte)DirectionType.Running);
    }

    /// <summary>
    /// Removes running flag from direction
    /// </summary>
    /// <param name="direction">Direction with or without running flag</param>
    /// <returns>Base direction without running flag</returns>
    public static DirectionType GetBaseDirection(DirectionType direction)
    {
        return (DirectionType)((byte)direction & (byte)DirectionType.Mask);
    }

    /// <summary>
    /// Addition operator for Point3D
    /// </summary>
    public static Point3D operator +(Point3D a, Point3D b)
    {
        return new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    /// <summary>
    /// Subtraction operator for Point3D
    /// </summary>
    public static Point3D operator -(Point3D a, Point3D b)
    {
        return new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    /// <summary>
    /// Addition operator for Point3D + DirectionType
    /// Moves the point in the specified direction
    /// </summary>
    public static Point3D operator +(Point3D point, DirectionType direction)
    {
        Point3D offset = direction; // Uses implicit conversion
        return point + offset;
    }

    /// <summary>
    /// Addition operator for DirectionType + Point3D
    /// Moves the point in the specified direction (commutative)
    /// </summary>
    public static Point3D operator +(DirectionType direction, Point3D point)
    {
        return point + direction; // Delegate to the other operator
    }

    /// <summary>
    /// Subtraction operator for Point3D - DirectionType
    /// Moves the point in the opposite direction
    /// </summary>
    public static Point3D operator -(Point3D point, DirectionType direction)
    {
        Point3D offset = direction; // Uses implicit conversion
        return point - offset;      // Move in opposite direction
    }


}
