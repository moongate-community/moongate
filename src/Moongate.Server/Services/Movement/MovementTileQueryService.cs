using Moongate.Server.Interfaces.Services.Movement;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Tiles;

namespace Moongate.Server.Services.Movement;

/// <summary>
/// Default tile query implementation backed by loaded map files.
/// </summary>
public sealed class MovementTileQueryService : IMovementTileQueryService
{
    public bool TryGetMapBounds(int mapId, out int width, out int height)
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            width = 0;
            height = 0;

            return false;
        }

        width = map.Width;
        height = map.Height;

        return true;
    }

    public bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile)
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            landTile = default;

            return false;
        }

        landTile = map.GetLandTile(x, y);

        return true;
    }

    public IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y)
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            return Array.Empty<StaticTile>();
        }

        var staticBlock = map.Tiles.GetStaticBlock(x >> 3, y >> 3);

        return staticBlock[x & 0x7][y & 0x7];
    }
}
