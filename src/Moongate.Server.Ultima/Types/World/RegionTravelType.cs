namespace Moongate.Server.Ultima.Types.World;

/// <summary>
///     The travel rules a region may switch off, as the flags of <c>data/regions</c>: a spell that moves a mobile into a
///     region or out of it asks for the rule that fits.
/// </summary>
public enum RegionTravelType
{
    /// <summary>
    ///     Recall may bring a mobile into the region.
    /// </summary>
    RecallIn = 0,

    /// <summary>
    ///     Recall may take a mobile out of the region.
    /// </summary>
    RecallOut = 1,

    /// <summary>
    ///     A gate may bring a mobile into the region.
    /// </summary>
    GateIn = 2,

    /// <summary>
    ///     A gate may take a mobile out of the region.
    /// </summary>
    GateOut = 3,

    /// <summary>
    ///     A rune may be marked in the region.
    /// </summary>
    Mark = 4,

    /// <summary>
    ///     Teleport may bring a mobile into the region.
    /// </summary>
    TeleportIn = 5,

    /// <summary>
    ///     Teleport may take a mobile out of the region.
    /// </summary>
    TeleportOut = 6
}
