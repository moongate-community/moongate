namespace Moongate.UO.Data.Hues;

/// <summary>
/// A UO color id stored on a tile, item or mobile. <see cref="Default" /> (0) means "no hue" and
/// renders the graphic with its native palette. This is the id only; the color table it indexes
/// lives in the Ultima hue reader.
/// </summary>
public readonly struct Hue : IEquatable<Hue>
{
    public static readonly Hue Default = new(0);

    public ushort Value { get; }

    public Hue(ushort value)
    {
        Value = value;
    }

    public bool IsDefault => Value == 0;

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

    public static implicit operator ushort(Hue hue)
    {
        return hue.Value;
    }

    public static explicit operator Hue(ushort value)
    {
        return new Hue(value);
    }

    public static bool operator ==(Hue left, Hue right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(Hue left, Hue right)
    {
        return left.Value != right.Value;
    }

    public override string ToString()
    {
        return $"0x{Value:X4}";
    }
}
