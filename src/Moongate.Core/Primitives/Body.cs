using System.Globalization;

namespace Moongate.Core.Primitives;

/// <summary>
///     The body id of a mobile: which figure the client draws, such as 400 for a male human. What kind of creature a
///     body is comes from <c>bodies.toml</c>; the facts below are fixed by the client and hold for every shard.
/// </summary>
public readonly struct Body : IEquatable<Body>
{
    public ushort Value { get; }

    /// <summary>
    ///     Gets whether this is a male body of a playable race, alive or as a ghost.
    /// </summary>
    public bool IsMale => Value is 183 or 185 or 400 or 402 or 605 or 607 or 666 or 694 or 750;

    /// <summary>
    ///     Gets whether this is a female body of a playable race, alive or as a ghost.
    /// </summary>
    public bool IsFemale => Value is 184 or 186 or 401 or 403 or 606 or 608 or 667 or 695 or 751 or 1253;

    /// <summary>
    ///     Gets whether this is the ghost body of a dead player character.
    /// </summary>
    public bool IsGhost => Value is 402 or 403 or 607 or 608 or 694 or 695 or 970;

    public bool IsGargoyle => Value is 666 or 667 or 694 or 695;

    public Body(ushort value)
    {
        Value = value;
    }

    public bool Equals(Body other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Body other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    /// <summary>
    ///     Writes the body id in hex, such as <c>0x0190</c>.
    /// </summary>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"0x{Value:X4}");
    }

    public static bool operator ==(Body left, Body right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Body left, Body right)
    {
        return !left.Equals(right);
    }
}
