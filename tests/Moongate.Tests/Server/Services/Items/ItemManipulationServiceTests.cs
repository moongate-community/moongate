using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Items;

public sealed class ItemManipulationServiceTests
{
    private sealed class ItemManipulationTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public bool EquipCalled { get; private set; }

        public void Add(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            _items.Remove(itemId);

            return Task.FromResult(true);
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
        {
            EquipCalled = true;

            if (_items.TryGetValue(itemId, out var item))
            {
                item.EquippedMobileId = mobileId;
                item.EquippedLayer = layer;
                item.ParentContainerId = Serial.Zero;
                item.ContainerPosition = Point2D.Zero;
            }

            return Task.FromResult(true);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult<List<UOItemEntity>>([]);

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(_items.Values.Where(item => item.ParentContainerId == containerId).ToList());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
        {
            _ = sessionId;

            if (!_items.TryGetValue(itemId, out var item) || !_items.TryGetValue(containerId, out var container))
            {
                return Task.FromResult(false);
            }

            if (item.ParentContainerId != Serial.Zero && _items.TryGetValue(item.ParentContainerId, out var oldContainer))
            {
                oldContainer.RemoveItem(item.Id);
            }

            container.AddItem(item, position);
            _items[item.Id] = item;
            _items[container.Id] = container;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => BulkUpsertItemsAsync(items);
    }

    private sealed class ItemManipulationTestMobileService : IMobileService
    {
        public UOMobileEntity? Mobile { get; set; }

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(Mobile);

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<List<UOMobileEntity>>([]);

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();
    }

    private sealed class ItemManipulationTestSpatialWorldService : ISpatialWorldService
    {
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

    [Test]
    public async Task HandleDropItemAsync_WhenDroppingAmmoIntoQuiver_ShouldMoveSingleAmmoStack()
    {
        var itemService = new ItemManipulationTestItemService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var dragService = new PlayerDragService();
        var spatialWorldService = new ItemManipulationTestSpatialWorldService();
        var mobileService = new ItemManipulationTestMobileService();
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();

        var characterId = (Serial)0x00000020u;
        var quiverId = (Serial)0x40000100u;
        var arrowsId = (Serial)0x40000101u;
        var quiver = new UOItemEntity
        {
            Id = quiverId,
            ItemId = 0x2B02,
            IsQuiver = true,
            EquippedMobileId = characterId,
            EquippedLayer = ItemLayerType.Cloak,
            GumpId = 0x0108
        };
        var arrows = new UOItemEntity
        {
            Id = arrowsId,
            ItemId = 0x0F3F,
            Amount = 20,
            IsStackable = true
        };
        itemService.Add(quiver);
        itemService.Add(arrows);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = characterId,
            Character = new() { Id = characterId, MapId = 0, Location = Point3D.Zero }
        };
        sessionService.Add(session);
        dragService.SetPending(session.SessionId, arrowsId, arrows.Amount, Serial.Zero, Point3D.Zero);

        var service = new ItemManipulationService(
            itemService,
            eventBus,
            dragService,
            spatialWorldService,
            mobileService,
            sessionService,
            outgoingQueue
        );

        var result = await service.HandleDropItemAsync(
                         session,
                         new()
                         {
                             ItemSerial = arrowsId,
                             DestinationSerial = quiverId,
                             Location = new(10, 20, 0)
                         }
                     );

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(arrows.ParentContainerId, Is.EqualTo(quiverId));
                Assert.That(quiver.ContainedItemIds, Is.EqualTo(new[] { arrowsId }));
                Assert.That(outgoingQueue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
            }
        );
    }

    [Test]
    public async Task HandleDropItemAsync_WhenDroppingNonAmmoIntoQuiver_ShouldRejectMove()
    {
        var itemService = new ItemManipulationTestItemService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var dragService = new PlayerDragService();
        var spatialWorldService = new ItemManipulationTestSpatialWorldService();
        var mobileService = new ItemManipulationTestMobileService();
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();

        var characterId = (Serial)0x00000020u;
        var quiverId = (Serial)0x40000110u;
        var swordId = (Serial)0x40000111u;
        var quiver = new UOItemEntity
        {
            Id = quiverId,
            ItemId = 0x2B02,
            IsQuiver = true,
            EquippedMobileId = characterId,
            EquippedLayer = ItemLayerType.Cloak
        };
        var sword = new UOItemEntity
        {
            Id = swordId,
            ItemId = 0x13B9,
            Amount = 1
        };
        itemService.Add(quiver);
        itemService.Add(sword);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = characterId,
            Character = new() { Id = characterId, MapId = 0, Location = Point3D.Zero }
        };
        sessionService.Add(session);
        dragService.SetPending(session.SessionId, swordId, 1, Serial.Zero, Point3D.Zero);

        var service = new ItemManipulationService(
            itemService,
            eventBus,
            dragService,
            spatialWorldService,
            mobileService,
            sessionService,
            outgoingQueue
        );

        var result = await service.HandleDropItemAsync(
                         session,
                         new()
                         {
                             ItemSerial = swordId,
                             DestinationSerial = quiverId,
                             Location = new(10, 20, 0)
                         }
                     );

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(sword.ParentContainerId, Is.EqualTo(Serial.Zero));
                Assert.That(quiver.ContainedItemIds, Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandleDropWearItemAsync_WhenWeaponIsEquipped_ShouldRefreshSessionCharacterAndStatusPacket()
    {
        var itemService = new ItemManipulationTestItemService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var dragService = new PlayerDragService();
        var spatialWorldService = new ItemManipulationTestSpatialWorldService();
        var mobileService = new ItemManipulationTestMobileService();
        var sessionService = new FakeGameNetworkSessionService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();

        var characterId = (Serial)0x00000020u;
        var bowId = (Serial)0x40000020u;
        var bow = new UOItemEntity
        {
            Id = bowId,
            ItemId = 0x13B2,
            CombatStats = new()
            {
                DamageMin = 18,
                DamageMax = 33,
                RangeMin = 1,
                RangeMax = 10
            },
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE
        };
        itemService.Add(bow);

        var persistedMobile = new UOMobileEntity
        {
            Id = characterId,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        persistedMobile.EquippedItemIds[ItemLayerType.TwoHanded] = bowId;
        mobileService.Mobile = persistedMobile;

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = characterId,
            Character = new()
            {
                Id = characterId,
                MapId = 0,
                Location = new(100, 100, 0),
                MinWeaponDamage = 1,
                MaxWeaponDamage = 4
            }
        };
        sessionService.Add(session);

        var service = new ItemManipulationService(
            itemService,
            eventBus,
            dragService,
            spatialWorldService,
            mobileService,
            sessionService,
            outgoingQueue
        );

        var packet = new DropWearItemPacket
        {
            ItemSerial = bowId,
            PlayerSerial = characterId,
            Layer = ItemLayerType.TwoHanded
        };

        var result = await service.HandleDropWearItemAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(itemService.EquipCalled, Is.True);
                Assert.That(session.Character, Is.SameAs(persistedMobile));
                Assert.That(session.Character!.MinWeaponDamage, Is.EqualTo(18));
                Assert.That(session.Character.MaxWeaponDamage, Is.EqualTo(33));
                Assert.That(session.Character.GetEquippedItemsRuntime().Single().Id, Is.EqualTo(bowId));
            }
        );

        var packets = new List<object>();

        while (outgoingQueue.TryDequeue(out var outbound))
        {
            packets.Add(outbound.Packet);
        }

        var statusPacket = packets.OfType<PlayerStatusPacket>().Single();

        Assert.That(statusPacket.Mobile, Is.SameAs(persistedMobile));
    }
}
