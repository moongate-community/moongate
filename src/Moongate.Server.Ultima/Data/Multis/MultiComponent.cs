using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Multis;

/// <summary>
///     One tile of a multi, such as a wall or floor piece of a house or a plank of a boat, placed relative to the
///     multi's centre.
/// </summary>
public readonly struct MultiComponent
{
    /// <summary>
    ///     Gets the item graphic id, the index into the item tiles of <c>ITileDataService</c>.
    /// </summary>
    public ushort ItemId { get; init; }

    /// <summary>
    ///     Gets the position relative to the multi's centre: X and Y in cells, Z in height units.
    /// </summary>
    public Point3D Offset { get; init; }

    /// <summary>
    ///     Gets whether the client draws the tile; hidden tiles, such as the centre marker, only take up space.
    /// </summary>
    public bool Visible { get; init; }
}
