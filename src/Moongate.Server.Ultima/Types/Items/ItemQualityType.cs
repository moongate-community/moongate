namespace Moongate.Server.Ultima.Types.Items;

/// <summary>
///     How well an item was crafted, as in ModernUO and ServUO; stored in the <c>quality</c> prop, an item without it
///     is <see cref="Regular" />.
/// </summary>
public enum ItemQualityType : byte
{
    /// <summary>
    ///     Poorly crafted: lower stats.
    /// </summary>
    Low = 0,

    /// <summary>
    ///     The usual quality.
    /// </summary>
    Regular = 1,

    /// <summary>
    ///     Crafted very well: better stats, and the crafter's name when there is one.
    /// </summary>
    Exceptional = 2
}
