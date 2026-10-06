namespace Moongate.Core.Attributes;

/// <summary>
///     Declares the serials an entity's rows may take, for entities whose range does not start at 1 (world items live in
///     <see cref="Primitives.Serial.MinItem" /> to <see cref="Primitives.Serial.MaxItem" />). A generated serial sequence
///     starts at <see cref="Min" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SerialRangeAttribute : Attribute
{
    /// <summary>
    ///     Gets the first serial of the range.
    /// </summary>
    public uint Min { get; }

    /// <summary>
    ///     Gets the last serial of the range.
    /// </summary>
    public uint Max { get; }

    /// <param name="min">
    ///     The first serial of the range.
    /// </param>
    /// <param name="max">
    ///     The last serial of the range.
    /// </param>
    public SerialRangeAttribute(uint min, uint max)
    {
        if (min == 0 || min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "A serial range needs 1 <= min <= max.");
        }

        Min = min;
        Max = max;
    }
}
