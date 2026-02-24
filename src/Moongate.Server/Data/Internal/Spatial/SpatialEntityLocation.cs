namespace Moongate.Server.Data.Internal.Spatial;

/// <summary>
/// Stores map and sector coordinates for an indexed entity.
/// </summary>
public sealed class SpatialEntityLocation
{
    /// <summary>
    /// Gets or sets the map id.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    /// Gets or sets the sector x coordinate.
    /// </summary>
    public required int SectorX { get; init; }

    /// <summary>
    /// Gets or sets the sector y coordinate.
    /// </summary>
    public required int SectorY { get; init; }
}
