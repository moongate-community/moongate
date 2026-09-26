using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Tiles;

/// <summary>
///     One terrain graphic from <c>tiledata.mul</c>, such as grass or water.
/// </summary>
public sealed class LandTile
{
    /// <summary>
    ///     Gets the land graphic id, from 0 to 0x3FFF.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    ///     Gets the name the client files give the terrain, such as <c>grass</c>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the tile flags, such as <see cref="TileFlagType.Impassable" /> or <see cref="TileFlagType.Wet" />.
    /// </summary>
    public TileFlagType Flags { get; init; }

    /// <summary>
    ///     Gets the id of the texture the client draws on stretched terrain.
    /// </summary>
    public ushort TextureId { get; init; }
}
