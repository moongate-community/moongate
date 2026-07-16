namespace Moongate.UO.Data.Bodies;

/// <summary>
/// The animation/graphic id of a mobile. Only the identity-level helpers that do not need the
/// tiledata body table live here (gender, ghost); richer classification can come later.
/// </summary>
public readonly struct Body : IEquatable<Body>
{
    public int Value { get; }

    public Body(int value)
    {
        Value = value;
    }

    /// <summary>True for the male human/elf/gargoyle player bodies.</summary>
    public bool IsMale => Value is 183 or 185 or 400 or 402 or 605 or 607 or 750 or 666 or 694;

    /// <summary>True for the female human/elf/gargoyle player bodies.</summary>
    public bool IsFemale => Value is 184 or 186 or 401 or 403 or 606 or 608 or 751 or 667 or 695 or 1253;

    /// <summary>True for the ghost bodies shown when a player is dead.</summary>
    public bool IsGhost => Value is 402 or 403 or 607 or 608 or 970;

    /// <summary>
    /// True for the human, elf and gargoyle bodies a player can wear — the ones that have a paperdoll.
    /// Derived from the male/female tables rather than a creature table, which we do not model.
    /// </summary>
    public bool IsHumanoid => IsMale || IsFemale;

    public bool Equals(Body other)
        => Value == other.Value;

    public override bool Equals(object? obj)
        => obj is Body other && Equals(other);

    public override int GetHashCode()
        => Value.GetHashCode();

    public static bool operator ==(Body left, Body right)
        => left.Value == right.Value;

    public static explicit operator Body(int value)
        => new(value);

    public static implicit operator int(Body body)
        => body.Value;

    public static bool operator !=(Body left, Body right)
        => left.Value != right.Value;

    public override string ToString()
        => $"0x{Value:X4}";
}
