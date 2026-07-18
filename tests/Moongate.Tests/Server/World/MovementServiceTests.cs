using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.World;

[Collection("UltimaClientData")]
public class MovementServiceTests
{
    private const ushort FlatLandId = 3;

    // Kept inside 0-31: UltimaFixtures.BuildTileData() allocates exactly one 32-item old-format
    // group, so an id >= 32 (the brief's original 100) writes past the buffer and
    // UltimaFixtures.SetItem throws ArgumentOutOfRangeException before the test body ever runs.
    // Same fix already applied in MapTileServiceTests.cs for the identical reason.
    private const ushort WallStaticId = 10;

    private static (MapTileService MapTiles, RegionService Regions) Build(bool withWall = false)
    {
        var tileData = UltimaFixtures.BuildTileData();
        UltimaFixtures.SetItem(tileData, WallStaticId, (uint)TileFlagType.Impassable, height: 20, name: "wall");

        var mapBlock = UltimaFixtures.BuildMapBlock(FlatLandId, 0);
        var files = new List<(string, byte[])> { ("map0.mul", mapBlock), ("tiledata.mul", tileData) };

        if (withWall)
        {
            var (index, statics) = UltimaFixtures.BuildStatics((WallStaticId, 2, 1, 0, 0));
            files.Add(("staidx0.mul", index));
            files.Add(("statics0.mul", statics));
        }

        var dir = UltimaFixtures.CreateClientDirectory(files.ToArray());
        Files.SetDirectory(dir);
        TileData.Initialize();

        var map = new Map(dir, 0, 0, 8, 8);
        var provider = new StubMapProvider(MapType.Felucca, map);

        return (new(provider), new());
    }

    private static MobileEntity Mobile(int x = 1, int y = 1, int z = 0, DirectionType direction = DirectionType.East)
        => new() { Id = new Serial(0x1), MapId = 0, Position = new(x, y, z), Direction = direction };

    [Fact]
    public void Evaluate_ValidStep_IsAcceptedAndResolvesPosition()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 0, null, DateTimeOffset.MinValue, now, mapTiles, regions, []);

        Assert.True(decision.Accepted);
        Assert.True(decision.PositionChanged);
        Assert.Equal(new Point3D(2, 1, 0), decision.NewPosition);
    }

    [Fact]
    public void Evaluate_SequenceMismatch_IsRejected()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 5, 0, now, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void Evaluate_TooSoonAfterLastMove_IsRejected()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var lastMoveAt = DateTimeOffset.UtcNow;
        var now = lastMoveAt.AddMilliseconds(100);

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 1, 0, lastMoveAt, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void Evaluate_TurnInPlace_AcceptedImmediately_NoTimingGate()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var lastMoveAt = DateTimeOffset.UtcNow;
        var now = lastMoveAt.AddMilliseconds(1); // far under the 400ms walk interval

        var decision = MovementService.Evaluate(mobile, DirectionType.South, 1, 0, lastMoveAt, now, mapTiles, regions, []);

        Assert.True(decision.Accepted);
        Assert.False(decision.PositionChanged);
        Assert.Equal(DirectionType.South, decision.NewDirection);
    }

    [Fact]
    public void Evaluate_ImpassableRegion_IsRejected()
    {
        var (mapTiles, regions) = Build();
        regions.Register(
            new()
            {
                Type = "TestRegion",
                Map = MapType.Felucca,
                Name = "Blocked",
                IsImpassable = true,
                Area = [new() { X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 }]
            }
        );
        var mobile = Mobile(direction: DirectionType.East);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 0, null, DateTimeOffset.MinValue, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void Evaluate_WallOnTargetTile_IsRejected()
    {
        var (mapTiles, regions) = Build(withWall: true);
        var mobile = Mobile(x: 1, y: 1, direction: DirectionType.East); // steps to (2, 1), where the wall sits

        var now = DateTimeOffset.UtcNow;
        var decision = MovementService.Evaluate(mobile, DirectionType.East, 0, null, DateTimeOffset.MinValue, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void Evaluate_AfterRejectionResetsSequence_StillEnforcesTimingAgainstPriorRealMove()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var lastRealMoveAt = DateTimeOffset.UtcNow;
        var now = lastRealMoveAt.AddMilliseconds(50); // well under the 400ms walk interval

        // Simulates: a real move was accepted at lastRealMoveAt, then a later packet was rejected
        // (e.g. sequence mismatch), which reset lastSequence to null but left lastMoveAt untouched
        // (TryMove's resync behavior). A subsequent attempt must still be timing-gated against the
        // real prior move, not treated as a fresh first-ever move.
        var decision = MovementService.Evaluate(mobile, DirectionType.East, 0, null, lastRealMoveAt, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }
}
