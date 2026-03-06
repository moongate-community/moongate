using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Scans world statics and computes potential door placements using hardcoded map regions.
/// </summary>
public sealed class DoorGeneratorBuilder : IWorldGenerator
{
    private const string DoorLinkSerialCustomFieldKey = "door_link_serial";

    private static readonly HashSet<int> EastFrames =
    [
        0x0007, 0x000A, 0x001A, 0x001C, 0x001E, 0x0037, 0x0058, 0x0059, 0x005C, 0x005E, 0x0080, 0x0081, 0x0082,
        0x0084, 0x0090, 0x0092, 0x0095, 0x0097, 0x0098, 0x00A6, 0x00A8, 0x00AB, 0x00AE, 0x00AF, 0x00B2, 0x00C7,
        0x00C8, 0x00EA, 0x00F8, 0x00F9, 0x00FC, 0x00FE, 0x00FF, 0x0102, 0x0104, 0x0105, 0x0108, 0x0127, 0x0128,
        0x012B, 0x012C, 0x012E, 0x0130, 0x0132, 0x0133, 0x0135, 0x0136, 0x0138, 0x013A, 0x014C, 0x014D, 0x014F,
        0x0150, 0x0152, 0x0154, 0x0156, 0x0158, 0x0159, 0x015C, 0x015E, 0x0160, 0x0163, 0x01CF, 0x01D0, 0x01D3,
        0x01FF, 0x0203, 0x0205, 0x0207, 0x0209
    ];

    private static readonly HashSet<int> NorthFrames =
    [
        0x0006, 0x0008, 0x000D, 0x001A, 0x001B, 0x0020, 0x003A, 0x0057, 0x0059, 0x005B, 0x005D, 0x0080, 0x0081,
        0x0082, 0x0084, 0x0090, 0x0091, 0x0094, 0x0096, 0x0099, 0x00A6, 0x00A7, 0x00AC, 0x00AE, 0x00B0, 0x00C7,
        0x00C9, 0x00F8, 0x00FA, 0x00FD, 0x00FE, 0x0100, 0x0103, 0x0104, 0x0106, 0x0109, 0x0127, 0x0129, 0x012B,
        0x012D, 0x012F, 0x0131, 0x0132, 0x0134, 0x0135, 0x0137, 0x0139, 0x013B, 0x014C, 0x014E, 0x014F, 0x0151,
        0x0153, 0x0155, 0x0157, 0x0158, 0x015A, 0x015D, 0x015E, 0x015F, 0x0162, 0x01CF, 0x01D1, 0x01D4, 0x01FF,
        0x0201, 0x0204, 0x0208, 0x020A
    ];

    private static readonly HashSet<int> SouthFrames =
    [
        0x0006, 0x0008, 0x000B, 0x001A, 0x001B, 0x001F, 0x0038, 0x0057, 0x0059, 0x005B, 0x005D, 0x0080, 0x0081,
        0x0082, 0x0084, 0x0090, 0x0091, 0x0094, 0x0096, 0x0099, 0x00A6, 0x00A7, 0x00AA, 0x00AE, 0x00B0, 0x00B3,
        0x00C7, 0x00C9, 0x00F8, 0x00FA, 0x00FD, 0x00FE, 0x0100, 0x0103, 0x0104, 0x0106, 0x0109, 0x0127, 0x0129,
        0x012B, 0x012D, 0x012F, 0x0131, 0x0132, 0x0134, 0x0135, 0x0137, 0x0139, 0x013B, 0x014C, 0x014E, 0x014F,
        0x0151, 0x0153, 0x0155, 0x0157, 0x0158, 0x015A, 0x015D, 0x015E, 0x015F, 0x0162, 0x01CF, 0x01D1, 0x01D4,
        0x01FF, 0x0204, 0x0206, 0x0208, 0x020A
    ];

    private static readonly HashSet<int> WestFrames =
    [
        0x0007, 0x000C, 0x001A, 0x001C, 0x0021, 0x0039, 0x0058, 0x0059, 0x005C, 0x005E, 0x0080, 0x0081, 0x0082,
        0x0084, 0x0090, 0x0092, 0x0095, 0x0097, 0x0098, 0x00A6, 0x00A8, 0x00AD, 0x00AE, 0x00AF, 0x00B5, 0x00C7,
        0x00C8, 0x00EA, 0x00F8, 0x00F9, 0x00FC, 0x00FE, 0x00FF, 0x0102, 0x0104, 0x0105, 0x0108, 0x0127, 0x0128,
        0x012C, 0x012E, 0x0130, 0x0132, 0x0133, 0x0135, 0x0136, 0x0138, 0x013A, 0x014C, 0x014D, 0x014F, 0x0150,
        0x0152, 0x0154, 0x0156, 0x0158, 0x0159, 0x015C, 0x015E, 0x0160, 0x0163, 0x01CF, 0x01D0, 0x01D3, 0x01FF,
        0x0200, 0x0203, 0x0207, 0x0209
    ];

    private readonly ILogger _logger = Log.ForContext<DoorGeneratorBuilder>();
    private readonly IReadOnlyList<DoorGenerationMapSpec> _mapSpecs;
    private readonly IMovementTileQueryService _tileQueryService;
    private readonly IItemService? _itemService;
    private readonly IItemFactoryService? _itemFactoryService;
    private int _nextDoorPairGroupId;

    public DoorGeneratorBuilder(
        IMovementTileQueryService tileQueryService,
        IItemService itemService,
        IItemFactoryService itemFactoryService,
        IDoorGenerationMapSpecProvider doorGenerationMapSpecProvider
    )
    {
        _tileQueryService = tileQueryService;
        _itemService = itemService;
        _itemFactoryService = itemFactoryService;
        _mapSpecs = doorGenerationMapSpecProvider.GetMapSpecs();
    }

    public string Name => "doors";

    public int LastGeneratedDoorCount { get; private set; }

    public IReadOnlyDictionary<int, int> LastGeneratedPerMap { get; private set; } = new Dictionary<int, int>();

    public IReadOnlyList<DoorGenerationPlacementRecord> LastGeneratedDoors { get; private set; } =
        Array.Empty<DoorGenerationPlacementRecord>();

    /// <summary>
    /// Generates and spawns door candidates around a center point within a square radius.
    /// </summary>
    /// <param name="mapId">Target map id.</param>
    /// <param name="center">Center location.</param>
    /// <param name="radius">Radius in tiles.</param>
    /// <param name="logCallback">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of spawned door candidates.</returns>
    public async Task<int> GenerateAroundAsync(
        int mapId,
        Point3D center,
        int radius,
        Action<string>? logCallback = null,
        CancellationToken cancellationToken = default
    )
    {
        _nextDoorPairGroupId = 1;

        if (!_tileQueryService.TryGetMapBounds(mapId, out var width, out var height))
        {
            logCallback?.Invoke($"Cannot scan doors around player: map {mapId} bounds unavailable.");

            return 0;
        }

        var clampedRadius = Math.Max(1, radius);
        var startX = Math.Max(0, center.X - clampedRadius);
        var startY = Math.Max(0, center.Y - clampedRadius);
        var endX = Math.Min(width, center.X + clampedRadius + 1);
        var endY = Math.Min(height, center.Y + clampedRadius + 1);

        var region = new Rectangle2D(new(startX, startY), new(endX, endY));
        var placements = new List<DoorGenerationPlacementRecord>();
        var occupiedDoorTiles = new HashSet<(int X, int Y, int Z)>();

        logCallback?.Invoke(
            $"Scanning doors around player on map {mapId} in area [{startX},{startY}] -> [{endX - 1},{endY - 1}] (radius={clampedRadius})."
        );

        var generated = AnalyzeRegion(
            mapId,
            region,
            width,
            height,
            occupiedDoorTiles,
            placements,
            cancellationToken
        );

        LastGeneratedDoorCount = generated;
        LastGeneratedPerMap = new Dictionary<int, int> { [mapId] = generated };
        LastGeneratedDoors = placements;

        await SpawnGeneratedDoorsAsync(placements);
        logCallback?.Invoke($"Door generation around player completed. Candidates: {generated}.");

        return generated;
    }

    /// <inheritdoc />
    public async Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default)
    {
        _nextDoorPairGroupId = 1;
        logCallback?.Invoke("Door generation started.");
        var mapCounts = new Dictionary<int, int>();
        var placements = new List<DoorGenerationPlacementRecord>();
        var total = 0;

        foreach (var mapSpec in _mapSpecs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_tileQueryService.TryGetMapBounds(mapSpec.MapId, out var width, out var height))
            {
                logCallback?.Invoke($"Skipping map {mapSpec.MapId}: bounds unavailable.");

                continue;
            }

            var generatedForMap = 0;
            var occupiedDoorTiles = new HashSet<(int X, int Y, int Z)>();
            logCallback?.Invoke($"Scanning doors on map {mapSpec.MapId}.");

            foreach (var region in mapSpec.Regions)
            {
                generatedForMap += AnalyzeRegion(
                    mapSpec.MapId,
                    region,
                    width,
                    height,
                    occupiedDoorTiles,
                    placements,
                    cancellationToken
                );
            }

            mapCounts[mapSpec.MapId] = generatedForMap;
            total += generatedForMap;
            logCallback?.Invoke($"Map {mapSpec.MapId}: {generatedForMap} door candidates.");
        }

        LastGeneratedDoorCount = total;
        LastGeneratedPerMap = mapCounts;
        LastGeneratedDoors = placements;

        _logger.Information("Door generation analysis completed. Total candidates: {DoorCount}", total);
        logCallback?.Invoke($"Door generation completed. Total candidates: {total}.");

        if (_itemService is null)
        {
            return;
        }

        await SpawnGeneratedDoorsAsync(placements);
    }

    private int AnalyzeRegion(
        int mapId,
        Rectangle2D region,
        int mapWidth,
        int mapHeight,
        HashSet<(int X, int Y, int Z)> occupiedDoorTiles,
        List<DoorGenerationPlacementRecord> placements,
        CancellationToken cancellationToken
    )
    {
        var generated = 0;

        for (var x = region.Start.X; x < region.End.X; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var y = region.Start.Y; y < region.End.Y; y++)
            {
                if (!IsInsideMap(x, y, mapWidth, mapHeight))
                {
                    continue;
                }

                var tiles = _tileQueryService.GetStaticTiles(mapId, x, y);

                foreach (var tile in tiles)
                {
                    var id = tile.ID;
                    var z = tile.Z;

                    if (WestFrames.Contains(id))
                    {
                        if (TryFindFrameAt(mapId, x + 2, y, z, mapWidth, mapHeight, EastFrames, out var newZ))
                        {
                            generated += TryAddCandidate(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
                                x + 1,
                                y,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.WestCW
                            );
                        }
                        else if (TryFindFrameAt(mapId, x + 3, y, z, mapWidth, mapHeight, EastFrames, out newZ))
                        {
                            generated += TryAddPairCandidates(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
                                x + 1,
                                y,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.WestCW,
                                x + 2,
                                y,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.EastCCW
                            );
                        }
                    }
                    else if (NorthFrames.Contains(id))
                    {
                        if (TryFindFrameAt(mapId, x, y + 2, z, mapWidth, mapHeight, SouthFrames, out var newZ))
                        {
                            generated += TryAddCandidate(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
                                x,
                                y + 1,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.SouthCW
                            );
                        }
                        else if (TryFindFrameAt(mapId, x, y + 3, z, mapWidth, mapHeight, SouthFrames, out newZ))
                        {
                            generated += TryAddPairCandidates(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
                                x,
                                y + 1,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.NorthCCW,
                                x,
                                y + 2,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.SouthCW
                            );
                        }
                    }
                }
            }
        }

        return generated;
    }

    private bool CanAddCandidate(
        int mapId,
        int mapWidth,
        int mapHeight,
        HashSet<(int X, int Y, int Z)> occupiedDoorTiles,
        int x,
        int y,
        int z
    )
    {
        if (!IsInsideMap(x, y, mapWidth, mapHeight) ||
            IsExcludedDoorLocation(mapId, x, y) ||
            !_tileQueryService.CanFit(
                mapId,
                x,
                y,
                z,
                16,
                false,
                false
            ) ||
            occupiedDoorTiles.Contains((x, y, z)))
        {
            return false;
        }

        return true;
    }

    private static bool IsExcludedDoorLocation(int mapId, int x, int y)
    {
        if (y == 1743 && x is >= 1343 and <= 1344)
        {
            return true;
        }

        if (y == 1679 && x is >= 1392 and <= 1393)
        {
            return true;
        }

        if (x == 1320 && y is >= 1618 and <= 1640)
        {
            return true;
        }

        if (x == 1383 && y is >= 1642 and <= 1643)
        {
            return true;
        }

        // Ilshenar ruins exclusion from ModernUO.
        if (mapId == 2 &&
            (x is >= 644 and <= 670 && y is >= 925 and <= 941 ||
             x == 985 && y == 994))
        {
            return true;
        }

        return false;
    }

    private static bool IsInsideMap(int x, int y, int mapWidth, int mapHeight)
        => x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;

    private async Task SpawnGeneratedDoorsAsync(IReadOnlyList<DoorGenerationPlacementRecord> placements)
    {
        if (_itemService is null || _itemFactoryService is null)
        {
            return;
        }

        var spawnedByPairGroup = new Dictionary<int, UOItemEntity>();
        var allDoors = new List<UOItemEntity>(placements.Count);

        foreach (var placement in placements)
        {
            var generatedDoor = _itemFactoryService.CreateItemFromTemplate("dark_wood_door");

            generatedDoor.Location = placement.Location;
            generatedDoor.MapId = placement.MapId;
            generatedDoor.Direction = placement.Facing.ToDirectionType();
            generatedDoor.ItemId = placement.Facing.ToItemId(generatedDoor.ItemId);

            if (placement.PairGroupId.HasValue)
            {
                var pairGroupId = placement.PairGroupId.Value;

                if (spawnedByPairGroup.TryGetValue(pairGroupId, out var linkedDoor))
                {
                    generatedDoor.SetCustomInteger(DoorLinkSerialCustomFieldKey, linkedDoor.Id.Value);
                    linkedDoor.SetCustomInteger(DoorLinkSerialCustomFieldKey, generatedDoor.Id.Value);
                }
                else
                {
                    spawnedByPairGroup[pairGroupId] = generatedDoor;
                }
            }

            allDoors.Add(generatedDoor);
        }

        await _itemService.BulkUpsertItemsAsync(allDoors);
    }

    private int TryAddCandidate(
        int mapId,
        int mapWidth,
        int mapHeight,
        HashSet<(int X, int Y, int Z)> occupiedDoorTiles,
        List<DoorGenerationPlacementRecord> placements,
        int x,
        int y,
        int z,
        DoorGenerationFacing facing
    )
    {
        if (!CanAddCandidate(mapId, mapWidth, mapHeight, occupiedDoorTiles, x, y, z))
        {
            return 0;
        }

        occupiedDoorTiles.Add((x, y, z));
        placements.Add(new(mapId, new(x, y, z), facing));

        return 1;
    }

    private int TryAddPairCandidates(
        int mapId,
        int mapWidth,
        int mapHeight,
        HashSet<(int X, int Y, int Z)> occupiedDoorTiles,
        List<DoorGenerationPlacementRecord> placements,
        int firstX,
        int firstY,
        int firstZ,
        DoorGenerationFacing firstFacing,
        int secondX,
        int secondY,
        int secondZ,
        DoorGenerationFacing secondFacing
    )
    {
        if (!CanAddCandidate(mapId, mapWidth, mapHeight, occupiedDoorTiles, firstX, firstY, firstZ) ||
            !CanAddCandidate(mapId, mapWidth, mapHeight, occupiedDoorTiles, secondX, secondY, secondZ))
        {
            return 0;
        }

        var pairGroupId = _nextDoorPairGroupId++;

        occupiedDoorTiles.Add((firstX, firstY, firstZ));
        placements.Add(new(mapId, new(firstX, firstY, firstZ), firstFacing, pairGroupId));

        occupiedDoorTiles.Add((secondX, secondY, secondZ));
        placements.Add(new(mapId, new(secondX, secondY, secondZ), secondFacing, pairGroupId));

        return 2;
    }

    private bool TryFindFrameAt(
        int mapId,
        int x,
        int y,
        int z,
        int mapWidth,
        int mapHeight,
        HashSet<int> frameSet,
        out int frameZ
    )
    {
        frameZ = 0;

        if (!IsInsideMap(x, y, mapWidth, mapHeight))
        {
            return false;
        }

        foreach (var tile in _tileQueryService.GetStaticTiles(mapId, x, y))
        {
            if (!frameSet.Contains(tile.ID))
            {
                continue;
            }

            if (Math.Abs(tile.Z - z) > 1)
            {
                continue;
            }

            frameZ = tile.Z;

            return true;
        }

        return false;
    }
}
