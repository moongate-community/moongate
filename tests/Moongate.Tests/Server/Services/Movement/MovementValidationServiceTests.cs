using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Movement;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Movement;

public class MovementValidationServiceTests
{
    [SetUp]
    public void SetUp()
    {
        TileData.LandTable[0] = new("walkable", UOTileFlag.None);
        TileData.LandTable[1] = new("blocked", UOTileFlag.Impassable);
        TileData.ItemTable[0x2000] = new(
            "floor",
            UOTileFlag.Surface,
            weight: 0,
            quality: 0,
            animation: 0,
            quantity: 0,
            value: 0,
            height: 20
        );
        TileData.ItemTable[0x2001] = new(
            "wall",
            UOTileFlag.Impassable | UOTileFlag.Surface,
            weight: 0,
            quality: 0,
            animation: 0,
            quantity: 0,
            value: 0,
            height: 20
        );
        TileData.ItemTable[0x2002] = new(
            "stair",
            UOTileFlag.Surface | UOTileFlag.Bridge,
            weight: 0,
            quality: 0,
            animation: 0,
            quantity: 0,
            value: 0,
            height: 10
        );
    }

    [Test]
    public void TryResolveMove_ShouldReturnFalse_WhenOutOfMapBounds()
    {
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true,
            Width = 64,
            Height = 64
        };
        var service = new MovementValidationService(tileQuery, new TestSpatialWorldService());
        var mobile = CreateMobile(new Point3D(0, 0, 0));

        var allowed = service.TryResolveMove(mobile, DirectionType.West, out _);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public void TryResolveMove_ShouldResolveZFromSurfaceTile()
    {
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true
        };
        tileQuery.StaticTiles[(11, 10)] = [new StaticTile(0x2000, 0)];

        var service = new MovementValidationService(tileQuery, new TestSpatialWorldService());
        var mobile = CreateMobile(new Point3D(10, 10, 18));

        var allowed = service.TryResolveMove(mobile, DirectionType.East, out var destination);

        Assert.Multiple(
            () =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(destination, Is.EqualTo(new Point3D(11, 10, 20)));
            }
        );
    }

    [Test]
    public void TryResolveMove_ShouldReturnFalse_WhenDestinationHasBlockingMobile()
    {
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true
        };
        tileQuery.StaticTiles[(11, 10)] = [new StaticTile(0x2000, 0)];
        var spatial = new TestSpatialWorldService();
        spatial.NearbyMobiles.Add(
            new UOMobileEntity
            {
                Id = (Serial)0x00000099,
                MapId = 1,
                Location = new Point3D(11, 10, 20)
            }
        );

        var service = new MovementValidationService(tileQuery, spatial);
        var mobile = CreateMobile(new Point3D(10, 10, 18));

        var allowed = service.TryResolveMove(mobile, DirectionType.East, out _);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public void TryResolveMove_ShouldReturnFalse_WhenDiagonalIsCornerBlocked()
    {
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true
        };
        tileQuery.StaticTiles[(11, 11)] = [new StaticTile(0x2000, 0)];
        tileQuery.LandTiles[(11, 10)] = new LandTile(1, 0); // impassable
        tileQuery.LandTiles[(10, 11)] = new LandTile(0, 0); // walkable

        var service = new MovementValidationService(tileQuery, new TestSpatialWorldService());
        var mobile = CreateMobile(new Point3D(10, 10, 0));

        var allowed = service.TryResolveMove(mobile, DirectionType.SouthEast, out _);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public void TryResolveMove_ShouldReturnFalse_WhenStaticOverlapsBodyAtDestination()
    {
        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true
        };
        tileQuery.StaticTiles[(11, 10)] = [new StaticTile(0x2001, 0)];

        var service = new MovementValidationService(tileQuery, new TestSpatialWorldService());
        var mobile = CreateMobile(new Point3D(10, 10, 0));

        var allowed = service.TryResolveMove(mobile, DirectionType.East, out _);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public void TryResolveMove_ShouldAllowBridgeStepUp_WhenStairChangesZ()
    {
        Assert.That(TileData.ItemTable[0x2002].Bridge, Is.True);

        var tileQuery = new TestMovementTileQueryService
        {
            HasMapBounds = true
        };
        tileQuery.StaticTiles[(11, 10)] = [new StaticTile(0x2002, 0)];

        var service = new MovementValidationService(tileQuery, new TestSpatialWorldService());
        var mobile = CreateMobile(new Point3D(10, 10, 0));

        var allowed = service.TryResolveMove(mobile, DirectionType.East, out var destination);

        Assert.Multiple(
            () =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(destination, Is.EqualTo(new Point3D(11, 10, 5)));
            }
        );
    }

    private static UOMobileEntity CreateMobile(Point3D location)
        => new()
        {
            Id = (Serial)0x00000001,
            MapId = 1,
            Location = location
        };

    private sealed class TestMovementTileQueryService : IMovementTileQueryService
    {
        public bool HasMapBounds { get; set; }

        public int Width { get; set; } = 6144;

        public int Height { get; set; } = 4096;

        public Dictionary<(int X, int Y), LandTile> LandTiles { get; } = [];

        public Dictionary<(int X, int Y), List<StaticTile>> StaticTiles { get; } = [];

        public bool TryGetMapBounds(int mapId, out int width, out int height)
        {
            width = Width;
            height = Height;

            return HasMapBounds;
        }

        public bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile)
        {
            if (LandTiles.TryGetValue((x, y), out var configured))
            {
                landTile = configured;

                return true;
            }

            landTile = new(0, 0);

            return true;
        }

        public IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y)
            => StaticTiles.TryGetValue((x, y), out var configured) ? configured : Array.Empty<StaticTile>();
    }

    private sealed class TestSpatialWorldService : ISpatialWorldService
    {
        public List<UOMobileEntity> NearbyMobiles { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => throw new NotImplementedException();

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => throw new NotImplementedException();

        public void AddRegion(JsonRegion region)
            => throw new NotImplementedException();

        public JsonRegion? GetRegionById(int regionId)
            => throw new NotImplementedException();

        public int GetMusic(int mapId, Point3D location)
            => throw new NotImplementedException();

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => throw new NotImplementedException();

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => NearbyMobiles;

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
            => throw new NotImplementedException();

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => throw new NotImplementedException();

        public List<MapSector> GetActiveSectors()
            => throw new NotImplementedException();

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => throw new NotImplementedException();

        public SectorSystemStats GetStats()
            => throw new NotImplementedException();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
            => throw new NotImplementedException();

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
            => throw new NotImplementedException();

        public void RemoveEntity(Serial serial)
            => throw new NotImplementedException();
    }
}
