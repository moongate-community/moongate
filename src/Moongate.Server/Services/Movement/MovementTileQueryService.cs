using Moongate.Server.Interfaces.Services.Movement;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

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

    public bool CanFit(int mapId, int x, int y, int z, int height = 16)
    {
        var map = Map.GetMap(mapId);

        if (map is null || x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return false;
        }

        var zTop = z + height;

        var landTile = map.GetLandTile(x, y);
        var landFlags = TileData.LandTable[NormalizeTileId(landTile.ID, TileData.LandTable.Length)].Flags;

        if ((landFlags & UOTileFlag.Impassable) != 0 && zTop > landTile.Z && landTile.Z >= z)
        {
            return false;
        }

        var staticBlock = map.Tiles.GetStaticBlock(x >> 3, y >> 3);

        foreach (var staticTile in staticBlock[x & 0x7][y & 0x7])
        {
            var itemData = TileData.ItemTable[NormalizeTileId(staticTile.ID, TileData.ItemTable.Length)];
            var isSurface = itemData[UOTileFlag.Surface];
            var isImpassable = itemData[UOTileFlag.Impassable];

            if (!isSurface && !isImpassable)
            {
                continue;
            }

            var tileTop = staticTile.Z + itemData.CalcHeight;

            if (tileTop > z && zTop > staticTile.Z)
            {
                return false;
            }
        }

        return true;
    }

    private static int NormalizeTileId(int tileId, int tableLength)
        => tableLength <= 0 ? 0 : ((tileId % tableLength) + tableLength) % tableLength;
}
