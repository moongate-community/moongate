namespace Moongate.Core.Primitives;

/// <summary>
/// The identity of a UO entity on the wire. Mobiles live in
/// [<see cref="MinMobile" />, <see cref="MaxMobile" />], items in
/// [<see cref="MinItem" />, <see cref="MaxItem" />], and virtual entities in
/// [<see cref="MinVirtual" />, <see cref="MaxVirtual" />]; zero is "no entity".
/// </summary>
public readonly struct Serial : IEquatable<Serial>, IComparable<Serial>
{
    public const uint MinMobile = 0x00000001;
    public const uint MaxMobile = 0x3FFFFFFF;
    public const uint MinItem = 0x40000000;

    /// <summary>
    /// The last serial a real item may take. It stops short of the top of the item range on purpose:
    /// everything above is reserved for virtual entities. ModernUO draws the same line, at the same place.
    /// </summary>
    public const uint MaxItem = 0x7EEEEEEE;

    /// <summary>
    /// Start of the band reserved for virtual entities — things the client must be able to identify but
    /// that are not entities on the server, such as hair. No real item is ever allocated here.
    /// </summary>
    public const uint MinVirtual = 0x7EEEEEEF;

    public const uint MaxVirtual = 0x7FFFFFFF;

    public static readonly Serial Zero = new(0);

    public uint Value { get; }

    public Serial(uint value)
    {
        Value = value;
    }

    public bool IsMobile => Value is >= MinMobile and <= MaxMobile;

    public bool IsItem => Value is >= MinItem and <= MaxItem;

    /// <summary>
    /// True for a serial handed out for a virtual entity. The client treats these as items; the server
    /// has nothing behind them, so looking one up in the item store finds nothing.
    /// </summary>
    public bool IsVirtual => Value is >= MinVirtual and <= MaxVirtual;

    public bool IsValid => Value != 0;

    public int CompareTo(Serial other)
        => Value.CompareTo(other.Value);

    public bool Equals(Serial other)
        => Value == other.Value;

    public override bool Equals(object? obj)
        => obj is Serial other && Equals(other);

    public override int GetHashCode()
        => Value.GetHashCode();

    public static bool operator ==(Serial left, Serial right)
        => left.Value == right.Value;

    public static explicit operator Serial(uint value)
        => new(value);

    public static bool operator >(Serial left, Serial right)
        => left.Value > right.Value;

    public static bool operator >=(Serial left, Serial right)
        => left.Value >= right.Value;

    public static implicit operator uint(Serial serial)
        => serial.Value;

    public static bool operator !=(Serial left, Serial right)
        => left.Value != right.Value;

    public static bool operator <(Serial left, Serial right)
        => left.Value < right.Value;

    public static bool operator <=(Serial left, Serial right)
        => left.Value <= right.Value;

    public override string ToString()
        => $"0x{Value:X8}";
}
