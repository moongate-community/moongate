using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.World;
using Moongate.UO.Data.Geometry;
using Serilog;

namespace Moongate.Server.Services.World;

/// <summary>
/// Scans world statics and computes potential door placements using hardcoded map regions.
/// </summary>
public sealed class DoorGeneratorBuilder : IWorldGenerator
{
    private static readonly HashSet<int> EastFrames = [0x0007, 0x000A, 0x001A, 0x001C, 0x001E, 0x0037, 0x0058];
    private static readonly HashSet<int> NorthFrames = [0x0006, 0x0008, 0x000D, 0x001A, 0x001B, 0x0020, 0x003A];
    private static readonly HashSet<int> SouthFrames = [0x0006, 0x0008, 0x000B, 0x001A, 0x001B, 0x001F, 0x0038];
    private static readonly HashSet<int> WestFrames = [0x0007, 0x000C, 0x001A, 0x001C, 0x0021, 0x0039, 0x0058];

    private static readonly IReadOnlyList<DoorGenerationMapSpec> DefaultMapSpecs =
    [
        new(
            1,
            [
                new(new Point2D(250, 750), new Point2D(775, 1330)),
                new(new Point2D(525, 2095), new Point2D(925, 2430)),
                new(new Point2D(1025, 2155), new Point2D(1265, 2310)),
                new(new Point2D(1635, 2430), new Point2D(1705, 2508)),
                new(new Point2D(1775, 2605), new Point2D(2165, 2975)),
                new(new Point2D(1055, 3520), new Point2D(1570, 4075)),
                new(new Point2D(2860, 3310), new Point2D(3120, 3630)),
                new(new Point2D(2470, 1855), new Point2D(3950, 3045)),
                new(new Point2D(3425, 990), new Point2D(3900, 1455)),
                new(new Point2D(4175, 735), new Point2D(4840, 1600)),
                new(new Point2D(2375, 330), new Point2D(3100, 1045)),
                new(new Point2D(2100, 1090), new Point2D(2310, 1450)),
                new(new Point2D(1495, 1400), new Point2D(1550, 1475)),
                new(new Point2D(1085, 1520), new Point2D(1415, 1910)),
                new(new Point2D(1410, 1500), new Point2D(1745, 1795)),
                new(new Point2D(5120, 2300), new Point2D(6143, 4095))
            ]
        ),
        new(
            0,
            [
                new(new Point2D(250, 750), new Point2D(775, 1330)),
                new(new Point2D(525, 2095), new Point2D(925, 2430)),
                new(new Point2D(1025, 2155), new Point2D(1265, 2310)),
                new(new Point2D(1635, 2430), new Point2D(1705, 2508)),
                new(new Point2D(1775, 2605), new Point2D(2165, 2975)),
                new(new Point2D(1055, 3520), new Point2D(1570, 4075)),
                new(new Point2D(2860, 3310), new Point2D(3120, 3630)),
                new(new Point2D(2470, 1855), new Point2D(3950, 3045)),
                new(new Point2D(3425, 990), new Point2D(3900, 1455)),
                new(new Point2D(4175, 735), new Point2D(4840, 1600)),
                new(new Point2D(2375, 330), new Point2D(3100, 1045)),
                new(new Point2D(2100, 1090), new Point2D(2310, 1450)),
                new(new Point2D(1495, 1400), new Point2D(1550, 1475)),
                new(new Point2D(1085, 1520), new Point2D(1415, 1910)),
                new(new Point2D(1410, 1500), new Point2D(1745, 1795)),
                new(new Point2D(5120, 2300), new Point2D(6143, 4095))
            ]
        ),
        new(2, [new(new Point2D(0, 0), new Point2D(288 * 8, 200 * 8))]),
        new(3, [new(new Point2D(0, 0), new Point2D(320 * 8, 256 * 8))])
    ];

    private readonly ILogger _logger = Log.ForContext<DoorGeneratorBuilder>();
    private readonly IReadOnlyList<DoorGenerationMapSpec> _mapSpecs;
    private readonly IMovementTileQueryService _tileQueryService;

    public DoorGeneratorBuilder(
        IMovementTileQueryService tileQueryService,
        IReadOnlyList<DoorGenerationMapSpec>? mapSpecs = null
    )
    {
        _tileQueryService = tileQueryService;
        _mapSpecs = mapSpecs.Count == 0 ? DefaultMapSpecs : mapSpecs;
    }

    public string Name => "doors";

    public int LastGeneratedDoorCount { get; private set; }

    public IReadOnlyDictionary<int, int> LastGeneratedPerMap { get; private set; } = new Dictionary<int, int>();

    public IReadOnlyList<DoorGenerationPlacementRecord> LastGeneratedDoors { get; private set; } =
        Array.Empty<DoorGenerationPlacementRecord>();

    /// <inheritdoc />
    public Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default)
    {
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
        // TODO: Persist generated scripted door items when door runtime behavior is implemented in Lua.

        return Task.CompletedTask;
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
                            generated += TryAddCandidate(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
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
                            generated += TryAddCandidate(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
                                x,
                                y + 1,
                                Math.Min(z, newZ),
                                DoorGenerationFacing.NorthCCW
                            );
                            generated += TryAddCandidate(
                                mapId,
                                mapWidth,
                                mapHeight,
                                occupiedDoorTiles,
                                placements,
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

    private static bool IsInsideMap(int x, int y, int mapWidth, int mapHeight)
        => x >= 0 && y >= 0 && x < mapWidth && y < mapHeight;

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
        if (!_tileQueryService.CanFit(mapId, x, y, z))
        {
            return 0;
        }

        if (!occupiedDoorTiles.Add((x, y, z)))
        {
            return 0;
        }

        placements.Add(new(mapId, new(x, y, z), facing));

        return 1;
    }
}

public readonly record struct DoorGenerationMapSpec(
    int MapId,
    IReadOnlyList<Rectangle2D> Regions
);

public readonly record struct DoorGenerationPlacementRecord(
    int MapId,
    Point3D Location,
    DoorGenerationFacing Facing
);

public enum DoorGenerationFacing
{
    WestCW = 0,
    EastCCW = 1,
    SouthCW = 2,
    NorthCCW = 3
}
