using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Movement;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Movement;

public sealed class MovementTileQueryServiceTests
{
    private static int _nextMapIndex = 200;

    private sealed class TestSpatialWorldService : ISpatialWorldService
    {
        public List<UOItemEntity> Items { get; } = [];
        public List<UOMobileEntity> Mobiles { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [.. Items.Where(item => item.MapId == mapId)];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [.. Mobiles.Where(mobile => mobile.MapId == mapId)];

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
            => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
            => [];

        public JsonRegion? GetRegionById(int regionId)
            => null;

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
            => null;

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    [Test]
    public void CanFit_ShouldReturnFalse_WhenBlockingMobileExistsAtLocation()
    {
        var mapId = RegisterTestMap();
        var spatial = new TestSpatialWorldService();
        spatial.Mobiles.Add(
            new()
            {
                Id = (Serial)0x00000010u,
                MapId = mapId,
                Location = new(12, 12, 0)
            }
        );

        var service = new MovementTileQueryService(spatial);
        var canFit = service.CanFit(
            mapId,
            12,
            12,
            0
        );

        Assert.That(canFit, Is.False);
    }

    [Test]
    public void CanFit_ShouldReturnFalse_WhenBlockingWorldItemExistsAtLocation()
    {
        var mapId = RegisterTestMap();
        var spatial = new TestSpatialWorldService();
        spatial.Items.Add(
            new()
            {
                Id = (Serial)0x40000010u,
                MapId = mapId,
                Location = new(10, 10, 0),
                ItemId = 0x2200
            }
        );

        var service = new MovementTileQueryService(spatial);
        var canFit = service.CanFit(
            mapId,
            10,
            10,
            0,
            16,
            false,
            false
        );

        Assert.That(canFit, Is.False);
    }

    [SetUp]
    public void SetUp()
    {
        TileData.LandTable[0] = new("walkable", UOTileFlag.None);
        TileData.ItemTable[0x2200] = new(
            "blocking_world_item",
            UOTileFlag.Surface | UOTileFlag.Impassable,
            0,
            0,
            0,
            0,
            0,
            20
        );
    }

    private static int RegisterTestMap()
    {
        var index = Interlocked.Increment(ref _nextMapIndex);
        Map.RegisterMap(
            index,
            index,
            index,
            64,
            64,
            SeasonType.Summer,
            $"test-{index}",
            MapRules.None
        );

        return index;
    }
}
