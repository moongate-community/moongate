namespace Moongate.Server.Ultima.Data.Maps;

/// <summary>
///     The terrain of one map cell: its land graphic and height.
/// </summary>
public readonly struct MapLandTile
{
    /// <summary>
    ///     Gets the land graphic id, the index into the land tiles of <c>ITileDataService</c>.
    /// </summary>
    public ushort Id { get; init; }

    /// <summary>
    ///     Gets the terrain height.
    /// </summary>
    public sbyte Z { get; init; }
}
