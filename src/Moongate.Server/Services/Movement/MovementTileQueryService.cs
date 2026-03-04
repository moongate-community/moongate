using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Movement;

/// <summary>
/// Default tile query implementation backed by loaded map files.
/// </summary>
public sealed class MovementTileQueryService : IMovementTileQueryService
{
    private readonly ISpatialWorldService _spatialWorldService;

    public MovementTileQueryService(ISpatialWorldService spatialWorldService)
    {
        _spatialWorldService = spatialWorldService;
    }

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

    public bool CanFit(
        int mapId,
        int x,
        int y,
        int z,
        int height = 16,
        bool checkBlocksFit = false,
        bool checkMobiles = true,
        bool requireSurface = true
    )
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            return false;
        }

        return CanFitCore(
            map,
            x,
            y,
            z,
            height,
            checkBlocksFit,
            checkMobiles,
            requireSurface,
            treatImpassableSurfaceAsSupport: false
        );
    }

    public bool CanFitItem(int mapId, int x, int y, int z, int height = 16)
    {
        var map = Map.GetMap(mapId);

        if (map is null)
        {
            return false;
        }

        return CanFitCore(
            map,
            x,
            y,
            z,
            height,
            checkBlocksFit: false,
            checkMobiles: false,
            requireSurface: true,
            treatImpassableSurfaceAsSupport: true
        );
    }

    private bool CanFitCore(
        Map map,
        int x,
        int y,
        int z,
        int height,
        bool checkBlocksFit,
        bool checkMobiles,
        bool requireSurface,
        bool treatImpassableSurfaceAsSupport
    )
    {
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
        {
            return false;
        }

        var zTop = z + height;
        var hasSurface = false;

        var landTile = map.GetLandTile(x, y);
        GetAverageZ(map, x, y, out var lowZ, out var avgZ, out _);
        var landFlags = TileData.LandTable[NormalizeTileId(landTile.ID, TileData.LandTable.Length)].Flags;

        if ((landFlags & UOTileFlag.Impassable) != 0 && avgZ > z && zTop > lowZ)
        {
            return false;
        }

        if ((landFlags & UOTileFlag.Impassable) == 0 && z == avgZ)
        {
            hasSurface = true;
        }

        var staticBlock = map.Tiles.GetStaticBlock(x >> 3, y >> 3);

        foreach (var staticTile in staticBlock[x & 0x7][y & 0x7])
        {
            var itemData = TileData.ItemTable[NormalizeTileId(staticTile.ID, TileData.ItemTable.Length)];
            var isSurface = itemData[UOTileFlag.Surface];
            var isImpassable = itemData[UOTileFlag.Impassable];
            var blocksFit = false;

            if (!isSurface && !isImpassable && !blocksFit)
            {
                continue;
            }

            var tileTop = staticTile.Z + itemData.CalcHeight;

            if (tileTop > z && zTop > staticTile.Z)
            {
                return false;
            }

            if (isSurface &&
                (treatImpassableSurfaceAsSupport || !isImpassable) &&
                z == tileTop)
            {
                hasSurface = true;
            }
        }

        var queryLocation = new Moongate.UO.Data.Geometry.Point3D(x, y, z);
        var worldItems = _spatialWorldService.GetNearbyItems(queryLocation, 0, map.MapID);

        foreach (var item in worldItems)
        {
            if (item.ParentContainerId != Moongate.UO.Data.Ids.Serial.Zero ||
                item.EquippedMobileId != Moongate.UO.Data.Ids.Serial.Zero ||
                item.Location.X != x ||
                item.Location.Y != y)
            {
                continue;
            }

            var itemData = TileData.ItemTable[NormalizeTileId(item.ItemId, TileData.ItemTable.Length)];
            var isSurface = itemData[UOTileFlag.Surface];
            var isImpassable = itemData[UOTileFlag.Impassable];
            var blocksFit = checkBlocksFit && itemData[UOTileFlag.Generic];

            if (!isSurface && !isImpassable && !blocksFit)
            {
                continue;
            }

            var itemTop = item.Location.Z + itemData.CalcHeight;

            if (itemTop > z && zTop > item.Location.Z)
            {
                return false;
            }

            if (isSurface &&
                (treatImpassableSurfaceAsSupport || !isImpassable) &&
                z == itemTop)
            {
                hasSurface = true;
            }
        }

        if (checkMobiles)
        {
            var nearbyMobiles = _spatialWorldService.GetNearbyMobiles(queryLocation, 0, map.MapID);

            foreach (var mobile in nearbyMobiles)
            {
                if (mobile.Location.X != x || mobile.Location.Y != y)
                {
                    continue;
                }

                if (mobile.Location.Z + 16 > z && zTop > mobile.Location.Z)
                {
                    return false;
                }
            }
        }

        return !requireSurface || hasSurface;
    }

    private static int NormalizeTileId(int tileId, int tableLength)
        => tableLength <= 0 ? 0 : ((tileId % tableLength) + tableLength) % tableLength;

    private static void GetAverageZ(Map map, int x, int y, out int lowZ, out int avgZ, out int topZ)
    {
        var z = map.GetLandTile(x, y).Z;
        var zTop = map.GetLandTile(Math.Min(x + 1, map.Width - 1), y).Z;
        var zLeft = map.GetLandTile(x, Math.Min(y + 1, map.Height - 1)).Z;
        var zRight = map.GetLandTile(
            Math.Min(x + 1, map.Width - 1),
            Math.Min(y + 1, map.Height - 1)
        ).Z;

        lowZ = Math.Min(Math.Min(z, zTop), Math.Min(zLeft, zRight));
        topZ = Math.Max(Math.Max(z, zTop), Math.Max(zLeft, zRight));

        if (Math.Abs(z - zRight) > Math.Abs(zTop - zLeft))
        {
            avgZ = (zTop + zLeft) / 2;
        }
        else
        {
            avgZ = (z + zRight) / 2;
        }
    }
}
