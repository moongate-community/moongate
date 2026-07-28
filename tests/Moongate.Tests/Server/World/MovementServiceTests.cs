using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;
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
        var decision = MovementService.Evaluate(
            mobile,
            DirectionType.East,
            0,
            null,
            lastRealMoveAt,
            now,
            mapTiles,
            regions,
            []
        );

        Assert.False(decision.Accepted);
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

        var decision = MovementService.Evaluate(
            mobile,
            DirectionType.East,
            0,
            null,
            DateTimeOffset.MinValue,
            now,
            mapTiles,
            regions,
            []
        );

        Assert.False(decision.Accepted);
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
    public void Evaluate_SequenceWrapsFrom255ButClientSendsZero_IsRejected()
    {
        var (mapTiles, regions) = Build();

        // Same turn-in-place setup as above; only the sequence value under test differs.
        var mobile = Mobile(direction: DirectionType.South);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 0, 255, now, now, mapTiles, regions, []);

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void Evaluate_SequenceWrapsFrom255To1_NotToZero()
    {
        var (mapTiles, regions) = Build();

        // Mobile faces South so the East move below is a turn-in-place, skipping the timing gate
        // entirely and leaving the sequence check as the only thing under test.
        var mobile = Mobile(direction: DirectionType.South);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(mobile, DirectionType.East, 1, 255, now, now, mapTiles, regions, []);

        Assert.True(decision.Accepted);
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
    public void Evaluate_ValidStep_IsAcceptedAndResolvesPosition()
    {
        var (mapTiles, regions) = Build();
        var mobile = Mobile(direction: DirectionType.East);
        var now = DateTimeOffset.UtcNow;

        var decision = MovementService.Evaluate(
            mobile,
            DirectionType.East,
            0,
            null,
            DateTimeOffset.MinValue,
            now,
            mapTiles,
            regions,
            []
        );

        Assert.True(decision.Accepted);
        Assert.True(decision.PositionChanged);
        Assert.Equal(new(2, 1, 0), decision.NewPosition);
    }

    [Fact]
    public void Evaluate_WallOnTargetTile_IsRejected()
    {
        var (mapTiles, regions) = Build(true);
        var mobile = Mobile(); // steps to (2, 1), where the wall sits

        var now = DateTimeOffset.UtcNow;
        var decision = MovementService.Evaluate(
            mobile,
            DirectionType.East,
            0,
            null,
            DateTimeOffset.MinValue,
            now,
            mapTiles,
            regions,
            []
        );

        Assert.False(decision.Accepted);
    }

    [Fact]
    public void TryMoveNpc_BrainMobile_StepsAndPublishesMovedEvent()
    {
        var (service, persistence, spatial, bus) = BuildMovementService();
        var mobile = Mobile();
        mobile.BrainScriptId = "guard";
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
        spatial.AddOrUpdate(mobile);

        Assert.True(service.TryMoveNpc(mobile.Id, DirectionType.East));

        var stored = persistence.Store<MobileEntity>().GetById(mobile.Id)!;
        Assert.Equal(new(2, 1, 0), stored.Position);
        var moved = Assert.Single(bus.Published.OfType<MobileMovedEvent>());
        Assert.Equal(mobile.Id, moved.Mobile);
        Assert.Equal((0, new Point3D(1, 1, 0)), (moved.FromMapId, moved.FromPosition));
        Assert.Equal((0, new Point3D(2, 1, 0)), (moved.ToMapId, moved.ToPosition));
    }

    [Fact]
    public void TryMoveNpc_MissingMobile_ReturnsFalse()
    {
        var (service, _, _, bus) = BuildMovementService();

        Assert.False(service.TryMoveNpc(new(0xDEAD), DirectionType.East));
        Assert.Empty(bus.Published.OfType<MobileMovedEvent>());
    }

    [Fact]
    public void TryMoveNpc_MobileWithoutBrain_ReturnsFalse()
    {
        var (service, persistence, spatial, bus) = BuildMovementService();
        var mobile = Mobile();
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
        spatial.AddOrUpdate(mobile);

        Assert.False(service.TryMoveNpc(mobile.Id, DirectionType.East));
        Assert.Empty(bus.Published.OfType<MobileMovedEvent>());
    }

    private static (MapTileService MapTiles, RegionService Regions) Build(bool withWall = false)
    {
        var tileData = UltimaFixtures.BuildTileData();
        UltimaFixtures.SetItem(tileData, WallStaticId, (uint)TileFlagType.Impassable, 20, "wall");

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

    private static (
        MovementService Service,
        FakePersistenceService Persistence,
        SpatialIndexService Spatial,
        StubEventBus Bus
        ) BuildMovementService()
    {
        var (mapTiles, regions) = Build();
        var persistence = new FakePersistenceService();
        var bus = new StubEventBus();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), bus);
        var world = new StubWorldService();

        return (new(mapTiles, regions, spatial, world, persistence, TimeProvider.System, bus, new StubLoopAffinity()),
                persistence, spatial, bus);
    }

    private static MobileEntity Mobile(int x = 1, int y = 1, int z = 0, DirectionType direction = DirectionType.East)
        => new() { Id = new(0x1), MapId = 0, Position = new(x, y, z), Direction = direction };
}
