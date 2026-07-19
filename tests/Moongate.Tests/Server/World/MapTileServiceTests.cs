using Moongate.Persistence.Entities;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Graphics;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.World;

[Collection("UltimaClientData")]
public class MapTileServiceTests
{
    private const ushort FlatLandId = 3;

    // Kept inside 0-31: UltimaFixtures.BuildTileData() allocates exactly one 32-item old-format
    // group, so an id >= 32 (the brief's original 100-103) writes past the buffer and
    // UltimaFixtures.SetItem throws ArgumentOutOfRangeException before the test body ever runs.
    // Confirmed against tests/Moongate.Tests/Ultima/TileDataTests.cs, whose passing item-id fixtures
    // (id 5) all stay under 32 too.
    private const ushort WallStaticId = 10;
    private const ushort PlatformStaticId = 11;
    private const ushort LowCeilingStaticId = 12;
    private const ushort ImpassableItemId = 13;
    private const ushort NearSurfaceId = 14;

    private static MapTileService Build(string dir)
    {
        Files.SetDirectory(dir);
        Art.Reload();
        TileData.Initialize();

        var map = new Map(dir, 0, 0, 8, 8);
        var provider = new StubMapProvider(MapType.Felucca, map);

        return new(provider);
    }

    private static string ClientDir(ushort landId, sbyte landZ, params (ushort Id, byte CellX, byte CellY, sbyte Z, ushort Hue)[] statics)
    {
        var tileData = UltimaFixtures.BuildTileData();

        // WallStaticId: a wall you cannot pass through and cannot stand on.
        UltimaFixtures.SetItem(tileData, WallStaticId, (uint)TileFlagType.Impassable, height: 20, name: "wall");

        // PlatformStaticId: a walkable surface with no obstruction of its own.
        UltimaFixtures.SetItem(tileData, PlatformStaticId, (uint)TileFlagType.Surface, height: 4, name: "platform");

        // LowCeilingStaticId: impassable, but its base sits above the ground, leaving too little headroom.
        UltimaFixtures.SetItem(tileData, LowCeilingStaticId, (uint)TileFlagType.Impassable, height: 4, name: "low ceiling");

        // ImpassableItemId: the dynamic (ground-item) counterpart to WallStaticId.
        UltimaFixtures.SetItem(tileData, ImpassableItemId, (uint)TileFlagType.Impassable, height: 20, name: "crate");

        // NearSurfaceId: a zero-height walkable surface, used to place a second candidate close to
        // currentZ that a separate obstacle then shadows, forcing the fallback loop to move on.
        UltimaFixtures.SetItem(tileData, NearSurfaceId, (uint)TileFlagType.Surface, height: 0, name: "near surface");

        var mapBlock = UltimaFixtures.BuildMapBlock(landId, landZ);
        var files = new List<(string, byte[])> { ("map0.mul", mapBlock), ("tiledata.mul", tileData) };

        if (statics.Length > 0)
        {
            var (index, staticsBytes) = UltimaFixtures.BuildStatics(statics);
            files.Add(("staidx0.mul", index));
            files.Add(("statics0.mul", staticsBytes));
        }

        return UltimaFixtures.CreateClientDirectory(files.ToArray());
    }

    [Fact]
    public void TryGetWalkableZ_FlatTerrain_ReturnsLandZ()
    {
        var dir = ClientDir(FlatLandId, landZ: 0);

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 0, groundItems: [], out var newZ);

            Assert.True(found);
            Assert.Equal(0, newZ);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_WallOnTheTile_ReturnsFalse()
    {
        var dir = ClientDir(FlatLandId, landZ: 0, (WallStaticId, 1, 1, 0, 0));

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 0, groundItems: [], out _);

            Assert.False(found);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_PlatformAtCurrentHeight_StaysOnPlatform()
    {
        // Ground is at Z=0, the platform static sits on it and is 4 units tall (top = 4). A mobile
        // already standing at Z=4 (elsewhere on the same platform) stepping onto this tile should stay
        // on the platform, not drop to the ground below.
        var dir = ClientDir(FlatLandId, landZ: 0, (PlatformStaticId, 1, 1, 0, 0));

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 4, groundItems: [], out var newZ);

            Assert.True(found);
            Assert.Equal(4, newZ);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_InsufficientHeadroom_ReturnsFalse()
    {
        // Ground at Z=0 is a valid candidate, but LowCeilingStaticId is impassable starting at Z=4,
        // leaving only 4 units of headroom — less than the 16-unit person height.
        var dir = ClientDir(FlatLandId, landZ: 0, (LowCeilingStaticId, 1, 1, 4, 0));

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 0, groundItems: [], out _);

            Assert.False(found);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_DynamicItemObstacle_ReturnsFalse()
    {
        var dir = ClientDir(FlatLandId, landZ: 0);

        try
        {
            var service = Build(dir);
            var groundItem = new ItemEntity { Id = new(0x40000001), ItemId = ImpassableItemId, MapId = 0, Position = new(1, 1, 0) };

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 0, groundItems: [groundItem], out _);

            Assert.False(found);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_UnknownMap_ReturnsFalse()
    {
        var dir = ClientDir(FlatLandId, landZ: 0);

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(mapId: 99, 1, 1, currentZ: 0, groundItems: [], out _);

            Assert.False(found);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_ClosestCandidateBlocked_FallsBackToFartherCandidate()
    {
        // Two candidates exist: NearSurfaceId at Z=18 (distance 2 from currentZ=20, the closer one)
        // and land at Z=0 (distance 20, the farther one). LowCeilingStaticId sits right above the
        // near surface (bottom=20), leaving only 2 units of headroom there — less than the 16-unit
        // person height — so the loop must skip it and fall back to land, which has a clear 20 units
        // of headroom underneath that same static.
        var dir = ClientDir(
            FlatLandId,
            landZ: 0,
            (NearSurfaceId, 1, 1, 18, 0),
            (LowCeilingStaticId, 1, 1, 20, 0)
        );

        try
        {
            var service = Build(dir);

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 20, groundItems: [], out var newZ);

            Assert.True(found);
            Assert.Equal(0, newZ);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryGetWalkableZ_GroundItemAtDifferentPosition_IsIgnored()
    {
        // ImpassableItemId would block (1,1) if it were actually there (see
        // TryGetWalkableZ_DynamicItemObstacle_ReturnsFalse), but here it sits at (2,2) — a wider
        // sweep the caller may pass without pre-filtering to the queried tile. It must be ignored.
        var dir = ClientDir(FlatLandId, landZ: 0);

        try
        {
            var service = Build(dir);
            var groundItem = new ItemEntity { Id = new(0x40000002), ItemId = ImpassableItemId, MapId = 0, Position = new(2, 2, 0) };

            var found = service.TryGetWalkableZ(0, 1, 1, currentZ: 0, groundItems: [groundItem], out var newZ);

            Assert.True(found);
            Assert.Equal(0, newZ);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
