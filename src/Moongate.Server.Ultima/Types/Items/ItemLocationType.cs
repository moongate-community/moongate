namespace Moongate.Server.Ultima.Types.Items;

/// <summary>
///     Where an item is: exactly one of these once it has been placed.
/// </summary>
public enum ItemLocationType : byte
{
    /// <summary>
    ///     Not placed yet; the database rejects saving it.
    /// </summary>
    None = 0,

    /// <summary>
    ///     On the ground of a map.
    /// </summary>
    Ground = 1,

    /// <summary>
    ///     Inside a container item.
    /// </summary>
    Container = 2,

    /// <summary>
    ///     Worn by a mobile on a layer.
    /// </summary>
    Equipped = 3
}
