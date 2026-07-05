namespace Moongate.Core.Primitives;

/// <summary>
/// The identity of a UO entity on the wire. Mobiles live in
/// [<see cref="MinMobile"/>, <see cref="MaxMobile"/>], items in
/// [<see cref="MinItem"/>, <see cref="MaxItem"/>]; zero is "no entity".
/// </summary>
public readonly struct Serial : IEquatable<Serial>, IComparable<Serial>
{
    public const uint MinMobile = 0x00000001;
    public const uint MaxMobile = 0x3FFFFFFF;
    public const uint MinItem = 0x40000000;
    public const uint MaxItem = 0x7FFFFFFF;

    public static readonly Serial Zero = new(0);

    public uint Value { get; }

    public Serial(uint value)
    {
        Value = value;
    }

    public bool IsMobile => Value is >= MinMobile and <= MaxMobile;

    public bool IsItem => Value is >= MinItem and <= MaxItem;

    public bool IsValid => Value != 0;

    public bool Equals(Serial other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Serial other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(Serial other)
    {
        return Value.CompareTo(other.Value);
    }

    public static implicit operator uint(Serial serial)
    {
        return serial.Value;
    }

    public static explicit operator Serial(uint value)
    {
        return new Serial(value);
    }

    public static bool operator ==(Serial left, Serial right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(Serial left, Serial right)
    {
        return left.Value != right.Value;
    }

    public static bool operator <(Serial left, Serial right)
    {
        return left.Value < right.Value;
    }

    public static bool operator >(Serial left, Serial right)
    {
        return left.Value > right.Value;
    }

    public static bool operator <=(Serial left, Serial right)
    {
        return left.Value <= right.Value;
    }

    public static bool operator >=(Serial left, Serial right)
    {
        return left.Value >= right.Value;
    }

    public override string ToString()
    {
        return $"0x{Value:X8}";
    }
}
