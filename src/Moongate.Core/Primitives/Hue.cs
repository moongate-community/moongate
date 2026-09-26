using System.Globalization;

namespace Moongate.Core.Primitives;

/// <summary>
///     A hue as the client sends and receives it: 16 bits, where 0 is "no hue" (the art's own colors). A set
///     <see cref="PartialFlag" /> asks the client to color only the gray pixels of the art, which is how skin is sent.
/// </summary>
public readonly struct Hue : IEquatable<Hue>
{
    /// <summary>
    ///     The bit that asks the client to apply the hue to the gray pixels only.
    /// </summary>
    public const ushort PartialFlag = 0x8000;

    public static readonly Hue None = new(0);

    public ushort Value { get; }

    /// <summary>
    ///     Gets whether this is "no hue", which leaves the art's own colors.
    /// </summary>
    public bool IsNone => Value == 0;

    /// <summary>
    ///     Gets whether the client applies this hue to the gray pixels of the art only.
    /// </summary>
    public bool IsPartial => (Value & PartialFlag) != 0;

    public Hue(ushort value)
    {
        Value = value;
    }

    public bool Equals(Hue other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Hue other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    /// <summary>
    ///     Writes the hue in hex, such as <c>0x83EA</c>.
    /// </summary>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{Value:X4}");
    }

    public static bool operator ==(Hue left, Hue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Hue left, Hue right)
    {
        return !left.Equals(right);
    }
}
