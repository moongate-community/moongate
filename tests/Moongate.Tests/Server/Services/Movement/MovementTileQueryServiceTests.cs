using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
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

    private sealed class TestDoorDataService : IDoorDataService
    {
        public Dictionary<int, DoorToggleDefinition> Definitions { get; } = [];

        public IReadOnlyList<DoorComponentEntry> GetAllEntries()
            => [];

        public void SetEntries(IReadOnlyList<DoorComponentEntry> entries)
            => _ = entries;

        public bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition)
            => Definitions.TryGetValue(itemId, out definition);
    }

    [Test]
    public void CanFit_ShouldIgnoreBlockingDoorItem_WhenDoorStateIsOpen()
    {
        var mapId = RegisterTestMap();
        var spatial = new TestSpatialWorldService();
        spatial.Items.Add(
            new()
            {
                Id = (Serial)0x40000011u,
                MapId = mapId,
                Location = new(20, 20, 0),
                ItemId = 0x3000
            }
        );

        var doorData = new TestDoorDataService();
        doorData.Definitions[0x3000] = new(0x3000, 0x3001, false, Point3D.Zero);
        var service = new MovementTileQueryService(spatial, doorData);
        var canFit = service.CanFit(
            mapId,
            20,
            20,
            0,
            16,
            false,
            false
        );

        Assert.That(canFit, Is.True);
    }

    [Test]
    public void CanFit_ShouldIgnoreStaticDoorAtClosedOrigin_WhenOpenedDoorMovedByOffset()
    {
        var mapId = RegisterTestMap();
        var map = Map.GetMap(mapId)!;
        var x = 24;
        var y = 24;

        var staticBlock = CreateEmptyStaticBlock();
        staticBlock[x & 0x7][y & 0x7] =
        [
            new(0x3100, 0)
            {
                X = x & 0x7,
                Y = y & 0x7
            }
        ];

        map.Tiles.SetStaticBlock(x >> 3, y >> 3, staticBlock);

        var spatial = new TestSpatialWorldService();
        spatial.Items.Add(
            new()
            {
                Id = (Serial)0x40000013u,
                MapId = mapId,

                // Door currently open and moved 1 tile east from its closed origin.
                Location = new(x + 1, y, 0),
                ItemId = 0x3000
            }
        );

        var doorData = new TestDoorDataService();
        doorData.Definitions[0x3000] = new(0x3000, 0x3001, false, new(1, 0, 0));
        var service = new MovementTileQueryService(spatial, doorData);

        var canFit = service.CanFit(
            mapId,
            x,
            y,
            0,
            16,
            false,
            false
        );

        Assert.That(canFit, Is.True);
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

        var service = new MovementTileQueryService(spatial, new TestDoorDataService());
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

        var service = new MovementTileQueryService(spatial, new TestDoorDataService());
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

    [Test]
    public void IsOpenedDoorCoveringTileForStaticCollision_ShouldMatchDoorClosedOrigin_WhenDoorIsOpen()
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000012u,
            MapId = 0,
            Location = new(11, 10, 0),
            ItemId = 0x3000
        };

        var doorData = new TestDoorDataService();
        doorData.Definitions[0x3000] = new(0x3000, 0x3001, false, new(1, 0, 0));

        var result = MovementTileQueryService.IsOpenedDoorCoveringTileForStaticCollision(
            [item],
            10,
            10,
            doorData
        );

        Assert.That(result, Is.True);
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
        TileData.ItemTable[0x3000] = new(
            "door_open_state_item",
            UOTileFlag.Surface | UOTileFlag.Impassable | UOTileFlag.Door,
            0,
            0,
            0,
            0,
            0,
            20
        );
    }

    private static StaticTile[][][] CreateEmptyStaticBlock()
    {
        var block = new StaticTile[8][][];

        for (var i = 0; i < 8; i++)
        {
            block[i] = new StaticTile[8][];

            for (var j = 0; j < 8; j++)
            {
                block[i][j] = [];
            }
        }

        return block;
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
