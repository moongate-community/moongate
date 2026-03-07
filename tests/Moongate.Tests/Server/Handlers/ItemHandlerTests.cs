using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
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

namespace Moongate.Tests.Server.Handlers;

public class ItemHandlerTests
{
    private sealed class ItemHandlerTestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> ItemsById { get; } = [];

        public bool EquipCalled { get; private set; }

        public Serial LastEquipItemId { get; private set; }

        public Serial LastEquipMobileId { get; private set; }

        public ItemLayerType LastEquipLayer { get; private set; }

        public bool EquipItemResult { get; set; }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
        {
            _ = generateNewSerial;

            if (!ItemsById.TryGetValue(itemId, out var source))
            {
                return Task.FromResult<UOItemEntity?>(null);
            }

            var nextIdValue = ItemsById.Count == 0 ? 0x40000010u : ItemsById.Keys.Max(static key => key.Value) + 1u;
            var clone = new UOItemEntity
            {
                Id = (Serial)nextIdValue,
                Location = source.Location,
                MapId = source.MapId,
                Name = source.Name,
                Weight = source.Weight,
                Amount = source.Amount,
                ItemId = source.ItemId,
                Hue = source.Hue,
                GumpId = source.GumpId,
                IsStackable = source.IsStackable,
                ScriptId = source.ScriptId,
                Rarity = source.Rarity,
                ParentContainerId = source.ParentContainerId,
                ContainerPosition = source.ContainerPosition,
                EquippedMobileId = source.EquippedMobileId,
                EquippedLayer = source.EquippedLayer
            };

            return Task.FromResult<UOItemEntity?>(clone);
        }

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            ItemsById.Remove(itemId);

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
            LastEquipItemId = itemId;
            LastEquipMobileId = mobileId;
            LastEquipLayer = layer;

            if (ItemsById.TryGetValue(itemId, out var item))
            {
                item.EquippedMobileId = mobileId;
                item.EquippedLayer = layer;
                item.ParentContainerId = Serial.Zero;
                item.ContainerPosition = Point2D.Zero;
                item.MapId = 0;
            }

            return Task.FromResult(EquipItemResult);
        }

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(ItemsById.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
        {
            ItemsById[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                ItemsById[item.Id] = item;
            }

            return Task.CompletedTask;
        }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class ItemHandlerTestMobileService : IMobileService
    {
        public UOMobileEntity? Mobile { get; set; }

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            return Task.FromResult(Mobile ?? new UOMobileEntity { Id = id, MapId = 0 });
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;
            _ = cancellationToken;

            return Task.FromResult<List<UOMobileEntity>>([]);
        }

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();
    }

    private sealed class ItemHandlerTestSpatialWorldService : ISpatialWorldService
    {
        public MapSector? Sector { get; set; }

        public List<UOMobileEntity> Players { get; } = [];
        public List<GameSession> SessionsInRange { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => _ = mobile;

        public void AddRegion(JsonRegion region)
            => _ = region;

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = packet;
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
        {
            _ = location;
            _ = range;
            _ = mapId;

            return excludeSession is null
                       ? [.. SessionsInRange]
                       : [.. SessionsInRange.Where(session => session != excludeSession)];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return Players;
        }

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return Sector;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
            => _ = serial;
    }

    private sealed class ItemHandlerTestScriptDispatcher : IItemScriptDispatcher
    {
        private readonly bool _hasHook;

        public ItemHandlerTestScriptDispatcher(bool hasHook)
        {
            _hasHook = hasHook;
        }

        public Task<bool> DispatchAsync(ItemScriptContext context, CancellationToken cancellationToken = default)
        {
            _ = context;
            _ = cancellationToken;

            return Task.FromResult(_hasHook);
        }

        public bool HasHook(UOItemEntity item, string hook)
        {
            _ = item;
            _ = hook;

            return _hasHook;
        }
    }

    [Test]
    public async Task HandleAsync_ItemMovedEvent_ShouldLoadCorrectItemAndContainer()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );

        var containerId = (Serial)0x40000001u;
        var itemId = (Serial)0x40000050u;

        itemService.ItemsById[containerId] = new()
        {
            Id = containerId,
            ItemId = 0x0E75,
            GumpId = 0x0042
        };
        itemService.ItemsById[itemId] = new()
        {
            Id = itemId,
            ItemId = 0x0EED,
            ParentContainerId = containerId
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u
            }
        };
        sessionService.Add(session);

        await handler.HandleAsync(
            new ItemMovedEvent(
                session.SessionId,
                itemId,
                Serial.Zero,
                containerId,
                new(0, 0, 0),
                new(10, 10, 0),
                0
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
                var draw = (DrawContainerAndAddItemCombinedPacket)outbound.Packet;
                Assert.That(draw.Container, Is.Not.Null);
                Assert.That(draw.Container!.Id, Is.EqualTo(containerId));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ItemAddedInSectorEvent_ShouldFilterBySessionAccountType()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var spatial = new ItemHandlerTestSpatialWorldService();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            spatial,
            new ItemHandlerTestMobileService()
        );

        var itemId = (Serial)0x40000061u;
        itemService.ItemsById[itemId] = new()
        {
            Id = itemId,
            ItemId = 0x0EED,
            Location = new(100, 100, 0),
            MapId = 1,
            Visibility = AccountType.GameMaster
        };

        using var regularClient = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var regularSession = new GameSession(new(regularClient))
        {
            CharacterId = (Serial)0x00000011u,
            Character = new()
            {
                Id = (Serial)0x00000011u,
                MapId = 1,
                Location = new(100, 100, 0)
            },
            AccountType = AccountType.Regular
        };
        sessionService.Add(regularSession);
        spatial.SessionsInRange.Add(regularSession);

        await handler.HandleAsync(new ItemAddedInSectorEvent(itemId, 1, 6, 6));

        Assert.That(queue.TryDequeue(out _), Is.False);
    }

    [Test]
    public async Task HandleAsync_ItemMovedEvent_WhenNoContainer_ShouldNotEnqueuePacket()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );

        var itemId = (Serial)0x40000050u;
        itemService.ItemsById[itemId] = new()
        {
            Id = itemId,
            ItemId = 0x0EED
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u
            }
        };
        sessionService.Add(session);

        await handler.HandleAsync(
            new ItemMovedEvent(
                session.SessionId,
                itemId,
                Serial.Zero,
                Serial.Zero,
                new(0, 0, 0),
                new(10, 10, 0),
                0
            )
        );

        Assert.That(queue.TryDequeue(out _), Is.False);
    }

    [Test]
    public async Task HandleAsync_WhenItemDeletedFromBackpack_ShouldRefreshBackpackContainer()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );

        var backpackId = (Serial)0x40000001u;
        itemService.ItemsById[backpackId] = new()
        {
            Id = backpackId,
            ItemId = 0x0E75,
            GumpId = 0x0042
        };

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                BackpackId = backpackId
            }
        };
        sessionService.Add(session);

        await handler.HandleAsync(
            new ItemDeletedEvent(
                0,
                (Serial)0x40000099u,
                backpackId,
                new(10, 10, 0),
                0
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
                var draw = (DrawContainerAndAddItemCombinedPacket)outbound.Packet;
                Assert.That(draw.Container, Is.Not.Null);
                Assert.That(draw.Container!.Id, Is.EqualTo(backpackId));
            }
        );
    }

    [Test]
    public async Task HandleAsync_WhenItemDeletedFromOwnBackpack_ShouldEarlyExitWithoutScanningAllSessions()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );

        var backpackId = (Serial)0x40000001u;
        itemService.ItemsById[backpackId] = new()
        {
            Id = backpackId,
            ItemId = 0x0E75,
            GumpId = 0x0042
        };

        using var sourceClient = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sourceSession = new GameSession(new(sourceClient))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                BackpackId = backpackId
            }
        };
        sessionService.Add(sourceSession);

        // Add a second session that should NOT be scanned in the fast path
        using var otherClient = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var otherSession = new GameSession(new(otherClient))
        {
            CharacterId = (Serial)0x00000003u,
            Character = new()
            {
                Id = (Serial)0x00000003u,
                BackpackId = (Serial)0x40000099u
            }
        };
        sessionService.Add(otherSession);

        await handler.HandleAsync(
            new ItemDeletedEvent(
                sourceSession.SessionId,
                (Serial)0x40000050u,
                backpackId,
                new(10, 10, 0),
                0
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(sourceSession.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());

                // Only one packet enqueued — the other session was not notified
                Assert.That(queue.TryDequeue(out _), Is.False);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishItemDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var targetSerial = (Serial)0x40000020u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            ParentContainerId = (Serial)0x40000001u
        };
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new DoubleClickPacket
        {
            TargetSerial = targetSerial
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var doubleClickEvent = eventBus.Events.OfType<ItemDoubleClickEvent>().FirstOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(doubleClickEvent.ItemSerial, Is.EqualTo(targetSerial));
                Assert.That(doubleClickEvent.SessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldPublishItemSingleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var targetSerial = (Serial)0x40000010u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            ParentContainerId = (Serial)0x40000001u
        };
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var packet = new SingleClickPacket
        {
            TargetSerial = targetSerial
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var singleClickEvent = eventBus.Events.OfType<ItemSingleClickEvent>().FirstOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(singleClickEvent.ItemSerial, Is.EqualTo(targetSerial));
                Assert.That(singleClickEvent.SessionId, Is.EqualTo(session.SessionId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDoubleClickGroundItemOutOfRangeAndRegular_ShouldNotPublishEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var targetSerial = (Serial)0x40000023u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            MapId = 0,
            Location = new(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        var session = new GameSession(new(client))
        {
            AccountType = AccountType.Regular,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 0,
                Location = new(10, 10, 0)
            }
        };

        var handled = await handler.HandlePacketAsync(
                          session,
                          new DoubleClickPacket
                          {
                              TargetSerial = targetSerial
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemDoubleClickEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDoubleClickItemWithoutScriptHook_ShouldNotPublishItemDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var targetSerial = (Serial)0x40000066u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            Name = "no-script-item",
            ScriptId = "none"
        };

        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService(),
            new ItemHandlerTestScriptDispatcher(false)
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));

        var handled = await handler.HandlePacketAsync(
                          session,
                          new DoubleClickPacket
                          {
                              TargetSerial = targetSerial
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemDoubleClickEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDoubleClickMobile_ShouldPublishMobileDoubleClickEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            new ItemHandlerTestItemService(),
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        var targetSerial = (Serial)0x00000099u;

        var handled = await handler.HandlePacketAsync(
                          session,
                          new DoubleClickPacket
                          {
                              TargetSerial = targetSerial
                          }
                      );

        var gameEvent = eventBus.Events.OfType<MobileDoubleClickEvent>().SingleOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.MobileSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDropStacksIntoExistingContainerStack_ShouldRefreshParentContainer()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var playerDragService = new PlayerDragService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            playerDragService,
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u
            }
        };

        var backpackId = (Serial)0x40000001u;
        var bankId = (Serial)0x40000002u;
        var draggedGoldId = (Serial)0x40000003u;
        var destinationGoldId = (Serial)0x40000004u;

        itemService.ItemsById[bankId] = new()
        {
            Id = bankId,
            ItemId = 0x09A8,
            GumpId = 0x0042
        };
        itemService.ItemsById[destinationGoldId] = new()
        {
            Id = destinationGoldId,
            ItemId = 0x0EED,
            Amount = 100,
            IsStackable = true,
            ParentContainerId = bankId,
            ContainerPosition = new(10, 10)
        };
        itemService.ItemsById[draggedGoldId] = new()
        {
            Id = draggedGoldId,
            ItemId = 0x0EED,
            Amount = 500,
            IsStackable = true,
            ParentContainerId = backpackId,
            ContainerPosition = new(20, 20)
        };

        playerDragService.SetPending(
            session.SessionId,
            draggedGoldId,
            500,
            backpackId,
            new(20, 20, 0)
        );

        var packet = new DropItemPacket
        {
            ItemSerial = draggedGoldId,
            DestinationSerial = destinationGoldId,
            Location = new(30, 30, 0)
        };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(itemService.ItemsById.ContainsKey(draggedGoldId), Is.False);
                Assert.That(itemService.ItemsById[destinationGoldId].Amount, Is.EqualTo(600));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
                var draw = (DrawContainerAndAddItemCombinedPacket)outbound.Packet;
                Assert.That(draw.Container, Is.Not.Null);
                Assert.That(draw.Container!.Id, Is.EqualTo(bankId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDropWearPacketIsValid_ShouldEquipItem()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService
        {
            EquipItemResult = true,
            ItemsById =
            {
                [(Serial)0x40000020u] = new()
                {
                    Id = (Serial)0x40000020u,
                    ItemId = 0x152E,
                    Hue = 0
                }
            }
        };
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new ItemHandlerTestSpatialWorldService
        {
            Sector = new(0, 0, 0)
        };
        spatialService.Players.Add(
            new()
            {
                Id = (Serial)0x00000002u,
                IsPlayer = true
            }
        );
        var mobileService = new ItemHandlerTestMobileService
        {
            Mobile = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 0,
                Location = Point3D.Zero,
                EquippedItemIds =
                {
                    [ItemLayerType.Pants] = (Serial)0x40000020u
                }
            }
        };
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            sessionService,
            new PlayerDragService(),
            spatialService,
            mobileService
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                Name = "player"
            }
        };
        sessionService.Add(session);
        var packet = new DropWearItemPacket
        {
            ItemSerial = (Serial)0x40000020u,
            Layer = ItemLayerType.Pants,
            PlayerSerial = (Serial)0x00000002u
        };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(itemService.EquipCalled, Is.True);
                Assert.That(itemService.LastEquipItemId, Is.EqualTo((Serial)0x40000020u));
                Assert.That(itemService.LastEquipMobileId, Is.EqualTo((Serial)0x00000002u));
                Assert.That(itemService.LastEquipLayer, Is.EqualTo(ItemLayerType.Pants));
                Assert.That(queue.TryDequeue(out var outgoing), Is.True);
                Assert.That(outgoing.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outgoing.Packet, Is.TypeOf<WornItemPacket>());
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenDropWearTargetPlayerDiffers_ShouldReject()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService
        {
            EquipItemResult = true
        };
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                Name = "player"
            }
        };
        var packet = new DropWearItemPacket
        {
            ItemSerial = (Serial)0x40000020u,
            Layer = ItemLayerType.Pants,
            PlayerSerial = (Serial)0x00000003u
        };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.False);
                Assert.That(itemService.EquipCalled, Is.False);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSingleClickGroundItemOutOfRangeAndGameMaster_ShouldPublishEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var targetSerial = (Serial)0x40000022u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            MapId = 0,
            Location = new(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        var session = new GameSession(new(client))
        {
            AccountType = AccountType.GameMaster,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 0,
                Location = new(10, 10, 0)
            }
        };

        var handled = await handler.HandlePacketAsync(
                          session,
                          new SingleClickPacket
                          {
                              TargetSerial = targetSerial
                          }
                      );
        var singleClickEvent = eventBus.Events.OfType<ItemSingleClickEvent>().FirstOrDefault();

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(singleClickEvent.ItemSerial, Is.EqualTo(targetSerial));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSingleClickGroundItemOutOfRangeAndRegular_ShouldNotPublishEvent()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            new PlayerDragService(),
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var targetSerial = (Serial)0x40000021u;
        itemService.ItemsById[targetSerial] = new()
        {
            Id = targetSerial,
            ItemId = 0x0EED,
            MapId = 0,
            Location = new(100, 100, 0),
            ParentContainerId = Serial.Zero,
            EquippedMobileId = Serial.Zero
        };
        var session = new GameSession(new(client))
        {
            AccountType = AccountType.Regular,
            Character = new()
            {
                Id = (Serial)0x00000002u,
                MapId = 0,
                Location = new(10, 10, 0)
            }
        };

        var handled = await handler.HandlePacketAsync(
                          session,
                          new SingleClickPacket
                          {
                              TargetSerial = targetSerial
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(eventBus.Events.OfType<ItemSingleClickEvent>(), Is.Empty);
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSplitStackPickUp_ShouldRefreshSourceContainer()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var playerDragService = new PlayerDragService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var handler = new ItemHandler(
            queue,
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            playerDragService,
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u
            }
        };

        var backpackId = (Serial)0x40000001u;
        var sourceGoldId = (Serial)0x40000003u;

        itemService.ItemsById[backpackId] = new()
        {
            Id = backpackId,
            ItemId = 0x0E75
        };
        itemService.ItemsById[sourceGoldId] = new()
        {
            Id = sourceGoldId,
            ItemId = 0x0EED,
            Amount = 1000,
            IsStackable = true,
            ParentContainerId = backpackId,
            ContainerPosition = new(10, 10),
            Location = new(10, 10, 0)
        };

        var handled = await handler.HandlePacketAsync(
                          session,
                          new PickUpItemPacket
                          {
                              ItemSerial = sourceGoldId,
                              StackAmount = 500
                          }
                      );

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<DrawContainerAndAddItemCombinedPacket>());
                var draw = (DrawContainerAndAddItemCombinedPacket)outbound.Packet;
                Assert.That(draw.Container, Is.Not.Null);
                Assert.That(draw.Container!.Id, Is.EqualTo(backpackId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_WhenSplitStackThenDropToBankStack_ShouldAcceptDropAndMergeAmount()
    {
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new ItemHandlerTestItemService();
        var playerDragService = new PlayerDragService();
        var handler = new ItemHandler(
            new BasePacketListenerTestOutgoingPacketQueue(),
            itemService,
            eventBus,
            new FakeGameNetworkSessionService(),
            playerDragService,
            new RegionDataLoaderTestSpatialWorldService(),
            new ItemHandlerTestMobileService()
        );
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002u,
            Character = new()
            {
                Id = (Serial)0x00000002u
            }
        };

        var backpackId = (Serial)0x40000001u;
        var bankId = (Serial)0x40000002u;
        var sourceGoldId = (Serial)0x40000003u;
        var bankGoldId = (Serial)0x40000004u;

        itemService.ItemsById[backpackId] = new()
        {
            Id = backpackId,
            ItemId = 0x0E75
        };
        itemService.ItemsById[bankId] = new()
        {
            Id = bankId,
            ItemId = 0x09A8,
            GumpId = 0x0042
        };
        itemService.ItemsById[sourceGoldId] = new()
        {
            Id = sourceGoldId,
            ItemId = 0x0EED,
            Amount = 1000,
            IsStackable = true,
            ParentContainerId = backpackId,
            ContainerPosition = new(10, 10),
            Location = new(10, 10, 0)
        };
        itemService.ItemsById[bankGoldId] = new()
        {
            Id = bankGoldId,
            ItemId = 0x0EED,
            Amount = 100,
            IsStackable = true,
            ParentContainerId = bankId,
            ContainerPosition = new(20, 20),
            Location = new(20, 20, 0)
        };

        var pickUpHandled = await handler.HandlePacketAsync(
                                session,
                                new PickUpItemPacket
                                {
                                    ItemSerial = sourceGoldId,
                                    StackAmount = 500
                                }
                            );

        var dropHandled = await handler.HandlePacketAsync(
                              session,
                              new DropItemPacket
                              {
                                  ItemSerial = sourceGoldId,
                                  DestinationSerial = bankGoldId,
                                  Location = new(30, 30, 0)
                              }
                          );

        Assert.Multiple(
            () =>
            {
                Assert.That(pickUpHandled, Is.True);
                Assert.That(dropHandled, Is.True);
                Assert.That(itemService.ItemsById[bankGoldId].Amount, Is.EqualTo(600));
                Assert.That(itemService.ItemsById.ContainsKey(sourceGoldId), Is.False);
            }
        );
    }
}
