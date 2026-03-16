using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Tiles;

namespace Moongate.Server.Utils;

public static class DoorPlacementUtils
{
    private const int DoorContextRange = 1;
    private const int MaxZDelta = 20;

    public static bool IsDoorFrameCandidate(ItemData tileData)
        => tileData.Wall || tileData.Impassable || tileData.Window || tileData.Door;

    public static DoorGenerationFacing ResolveFacing(
        IMovementTileQueryService movementTileQueryService,
        ISpatialWorldService spatialWorldService,
        int mapId,
        Point3D location
    )
    {
        var westScore =
            ScoreSide(movementTileQueryService, spatialWorldService, mapId, location, location.X - 1, location.Y);
        var eastScore =
            ScoreSide(movementTileQueryService, spatialWorldService, mapId, location, location.X + 1, location.Y);
        var northScore = ScoreSide(
            movementTileQueryService,
            spatialWorldService,
            mapId,
            location,
            location.X,
            location.Y - 1
        );
        var southScore = ScoreSide(
            movementTileQueryService,
            spatialWorldService,
            mapId,
            location,
            location.X,
            location.Y + 1
        );

        var horizontalScore = Math.Max(westScore, eastScore);
        var verticalScore = Math.Max(northScore, southScore);

        if (horizontalScore > verticalScore)
        {
            return westScore >= eastScore ? DoorGenerationFacing.WestCW : DoorGenerationFacing.EastCCW;
        }

        if (verticalScore > horizontalScore)
        {
            return southScore >= northScore ? DoorGenerationFacing.SouthCW : DoorGenerationFacing.NorthCCW;
        }

        if (horizontalScore > 0)
        {
            return westScore >= eastScore ? DoorGenerationFacing.WestCW : DoorGenerationFacing.EastCCW;
        }

        if (verticalScore > 0)
        {
            return southScore >= northScore ? DoorGenerationFacing.SouthCW : DoorGenerationFacing.NorthCCW;
        }

        return DoorGenerationFacing.WestCW;
    }

    private static int ScoreSide(
        IMovementTileQueryService movementTileQueryService,
        ISpatialWorldService spatialWorldService,
        int mapId,
        Point3D origin,
        int x,
        int y
    )
    {
        var score = 0;

        foreach (var tile in movementTileQueryService.GetStaticTiles(mapId, x, y))
        {
            if (Math.Abs(tile.Z - origin.Z) > MaxZDelta)
            {
                continue;
            }

            var tileData = TileData.ItemTable[tile.ID & TileData.MaxItemValue];

            if (IsDoorFrameCandidate(tileData))
            {
                score++;
            }
        }

        foreach (var item in spatialWorldService.GetNearbyItems(origin, DoorContextRange, mapId))
        {
            if (item.Location.X != x || item.Location.Y != y || Math.Abs(item.Location.Z - origin.Z) > MaxZDelta)
            {
                continue;
            }

            var tileData = TileData.ItemTable[item.ItemId & TileData.MaxItemValue];

            if (IsDoorFrameCandidate(tileData))
            {
                score++;
            }
        }

        return score;
    }
}
