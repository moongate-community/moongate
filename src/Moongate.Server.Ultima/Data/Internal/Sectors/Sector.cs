using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Sectors;

/// <summary>
///     A 16×16 block of one map, the mobiles standing in it and the items lying on its ground.
/// </summary>
public sealed class Sector
{
    public MapType Map { get; }

    /// <summary>
    ///     Gets the sector's column: its first cell's X divided by 16.
    /// </summary>
    public int X { get; }

    /// <summary>
    ///     Gets the sector's row: its first cell's Y divided by 16.
    /// </summary>
    public int Y { get; }

    /// <summary>
    ///     Gets or sets how many players stand within two sectors of this one; above zero the sector is active.
    /// </summary>
    public int NearbyPlayers { get; set; }

    public List<MobileEntity> Mobiles { get; } = [];

    public List<ItemEntity> Items { get; } = [];

    public Sector(MapType map, int x, int y)
    {
        Map = map;
        X = x;
        Y = y;
    }
}
