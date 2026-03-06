using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public sealed class DoorServiceTests
{
    private sealed class DoorServiceTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];
        public int MoveToWorldCalls { get; private set; }
        public int UpsertCalls { get; private set; }

        public DoorServiceTestItemService(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult((Serial)1u);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            if (!_items.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            MoveToWorldCalls++;
            item.Location = location;
            item.MapId = mapId;

            return Task.FromResult(true);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? (true, item) : (false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertCalls++;
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class DoorServiceTestSpatialWorldService : ISpatialWorldService
    {
        public int AddOrUpdateCalls { get; private set; }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => AddOrUpdateCalls++;

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
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

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

    private sealed class DoorServiceTestDoorDataService : IDoorDataService
    {
        private readonly Dictionary<int, DoorToggleDefinition> _definitions = [];

        public static DoorServiceTestDoorDataService CreateDefault()
        {
            var service = new DoorServiceTestDoorDataService();
            service.SetEntries(
                [
                    new(
                        0,
                        0x0679,
                        0x067B,
                        0x0675,
                        0x0677,
                        0x067D,
                        0x067F,
                        0x0681,
                        0x0683,
                        0,
                        "Metal Door"
                    ),
                    new(
                        1,
                        0x0689,
                        0x068B,
                        0x0685,
                        0x0687,
                        0x068D,
                        0x068F,
                        0x0691,
                        0x0693,
                        0,
                        "Barred Metal Door"
                    )
                ]
            );

            return service;
        }

        public IReadOnlyList<DoorComponentEntry> GetAllEntries()
            => [];

        public void SetEntries(IReadOnlyList<DoorComponentEntry> entries)
        {
            _definitions.Clear();

            foreach (var entry in entries)
            {
                var pieces = new[]
                {
                    entry.Piece1, entry.Piece2, entry.Piece3, entry.Piece4, entry.Piece5, entry.Piece6, entry.Piece7,
                    entry.Piece8
                };
                var pieceToFacing = new[] { 2, 3, 0, 1, 4, 5, 6, 7 };
                var offsets = new[]
                {
                    new Point3D(-1, 1, 0),
                    new Point3D(1, 1, 0),
                    new Point3D(-1, 0, 0),
                    new Point3D(1, -1, 0),
                    new Point3D(1, 1, 0),
                    new Point3D(1, -1, 0),
                    new Point3D(0, 0, 0),
                    new Point3D(0, -1, 0)
                };

                for (var i = 0; i < 8; i++)
                {
                    var closedId = pieces[i];

                    if (closedId <= 0)
                    {
                        continue;
                    }

                    var openedId = closedId + 1;
                    var offset = offsets[pieceToFacing[i]];
                    _definitions[closedId] = new(closedId, openedId, true, offset);
                    _definitions[openedId] = new(openedId, closedId, false, offset);

                    if (!_definitions.ContainsKey(closedId - 1))
                    {
                        _definitions[closedId - 1] = new(closedId - 1, openedId, true, offset);
                    }
                }
            }
        }

        public bool TryGetToggleDefinition(int itemId, out DoorToggleDefinition definition)
            => _definitions.TryGetValue(itemId, out definition);
    }

    [Test]
    public async Task IsDoorAsync_WhenItemIsDoor_ShouldReturnTrue()
    {
        var item = CreateWorldItem((Serial)0x40000001u, 0x0685, new(100, 100, 0));
        var itemService = new DoorServiceTestItemService(item);
        var spatial = new DoorServiceTestSpatialWorldService();
        var doorData = DoorServiceTestDoorDataService.CreateDefault();
        var service = new DoorService(itemService, spatial, doorData);

        var result = await service.IsDoorAsync(item.Id);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ToggleAsync_WhenClosedDoor_ShouldOpenAndMove()
    {
        var item = CreateWorldItem((Serial)0x40000001u, 0x0685, new(100, 100, 0));
        var itemService = new DoorServiceTestItemService(item);
        var spatial = new DoorServiceTestSpatialWorldService();
        var doorData = DoorServiceTestDoorDataService.CreateDefault();
        var service = new DoorService(itemService, spatial, doorData);

        var result = await service.ToggleAsync(item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(item.ItemId, Is.EqualTo(0x0686));
                Assert.That(item.Location, Is.EqualTo(new Point3D(99, 101, 0)));
                Assert.That(itemService.MoveToWorldCalls, Is.EqualTo(1));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(1));
                Assert.That(spatial.AddOrUpdateCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task ToggleAsync_WhenDoorIsLinked_ShouldToggleBothDoors()
    {
        var firstDoor = CreateWorldItem((Serial)0x40000001u, 0x0685, new(100, 100, 0));
        var secondDoor = CreateWorldItem((Serial)0x40000002u, 0x0687, new(110, 110, 0));
        firstDoor.SetCustomInteger("door_link_serial", (uint)secondDoor.Id);
        secondDoor.SetCustomInteger("door_link_serial", (uint)firstDoor.Id);

        var itemService = new DoorServiceTestItemService(firstDoor, secondDoor);
        var spatial = new DoorServiceTestSpatialWorldService();
        var doorData = DoorServiceTestDoorDataService.CreateDefault();
        var service = new DoorService(itemService, spatial, doorData);

        var result = await service.ToggleAsync(firstDoor.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(firstDoor.ItemId, Is.EqualTo(0x0686));
                Assert.That(firstDoor.Location, Is.EqualTo(new Point3D(99, 101, 0)));
                Assert.That(secondDoor.ItemId, Is.EqualTo(0x0688));
                Assert.That(secondDoor.Location, Is.EqualTo(new Point3D(111, 111, 0)));
                Assert.That(itemService.MoveToWorldCalls, Is.EqualTo(2));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(2));
                Assert.That(spatial.AddOrUpdateCalls, Is.EqualTo(2));
            }
        );
    }

    [Test]
    public async Task ToggleAsync_WhenItemIsNotDoor_ShouldReturnFalse()
    {
        var item = CreateWorldItem((Serial)0x40000001u, 0x0EED, new(100, 100, 0));
        var itemService = new DoorServiceTestItemService(item);
        var spatial = new DoorServiceTestSpatialWorldService();
        var doorData = DoorServiceTestDoorDataService.CreateDefault();
        var service = new DoorService(itemService, spatial, doorData);

        var result = await service.ToggleAsync(item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.False);
                Assert.That(itemService.MoveToWorldCalls, Is.EqualTo(0));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(0));
                Assert.That(spatial.AddOrUpdateCalls, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public async Task ToggleAsync_WhenLegacyOffByOneDoorId_ShouldRecoverAndOpen()
    {
        var item = CreateWorldItem((Serial)0x40000001u, 0x0674, new(100, 100, 0));
        var itemService = new DoorServiceTestItemService(item);
        var spatial = new DoorServiceTestSpatialWorldService();
        var doorData = DoorServiceTestDoorDataService.CreateDefault();
        var service = new DoorService(itemService, spatial, doorData);

        var result = await service.ToggleAsync(item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(item.ItemId, Is.EqualTo(0x0676));
                Assert.That(item.Location, Is.EqualTo(new Point3D(99, 101, 0)));
            }
        );
    }

    private static UOItemEntity CreateWorldItem(Serial id, int itemId, Point3D location)
        => new()
        {
            Id = id,
            ItemId = itemId,
            MapId = 0,
            Location = location,
            Name = "door",
            ScriptId = "items.door"
        };
}
