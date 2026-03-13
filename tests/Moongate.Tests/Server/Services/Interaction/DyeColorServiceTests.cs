using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class DyeColorServiceTests
{
    private sealed class DyeColorServiceTestPlayerTargetService : IPlayerTargetService
    {
        public Action<Moongate.Server.Data.Internal.Cursors.PendingCursorCallback>? PendingCallback { get; private set; }
        public long LastSessionId { get; private set; }

        public Task HandleAsync(Moongate.Server.Data.Events.Targeting.TargetRequestCursorEvent gameEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HandlePacketAsync(GameSession session, Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet)
            => Task.FromResult(true);

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
            => Task.CompletedTask;

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<Moongate.Server.Data.Internal.Cursors.PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            LastSessionId = sessionId;
            PendingCallback = callback;

            return Task.FromResult((Serial)0x4000ABCDu);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class DyeColorServiceTestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> Items { get; } = [];
        public UOItemEntity? LastUpsertedItem { get; private set; }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(false);

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult<Moongate.Server.Data.Items.DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(false);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(Items.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(Items.Values.Where(item => item.ParentContainerId == containerId).ToList());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult(Items.TryGetValue(itemId, out var item) ? (true, item) : (false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            Items[item.Id] = item;
            LastUpsertedItem = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class DyeColorServiceTestSpatialWorldService : ISpatialWorldService
    {
        public UOItemEntity? LastUpdatedItem { get; private set; }
        public Moongate.Network.Packets.Interfaces.IGameNetworkPacket? LastBroadcastPacket { get; private set; }
        public int LastBroadcastMapId { get; private set; }
        public Point3D LastBroadcastLocation { get; private set; }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => LastUpdatedItem = item;

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }
        public void AddRegion(Moongate.UO.Data.Json.Regions.JsonRegion region) { }
        public Task<int> BroadcastToPlayersAsync(Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet, int mapId, Point3D location, int? range = null, long? excludeSessionId = null)
        {
            LastBroadcastPacket = packet;
            LastBroadcastMapId = mapId;
            LastBroadcastLocation = location;

            return Task.FromResult(1);
        }
        public Task<int> BroadcastToPlayersInSectorRangeAsync(Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet, int mapId, int centerSectorX, int centerSectorY, int sectorRadius = 0, long? excludeSessionId = null)
            => Task.FromResult(0);
        public Task<int> BroadcastToPlayersInUpdateRadiusAsync(Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet, int mapId, Point3D location, long? excludeSessionId = null)
            => BroadcastToPlayersAsync(packet, mapId, location, null, excludeSessionId);
        public List<Moongate.UO.Data.Maps.MapSector> GetActiveSectors() => [];
        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2) => [];
        public int GetMusic(int mapId, Point3D location) => 0;
        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId) => [];
        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId) => [];
        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null) => [];
        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY) => [];
        public Moongate.UO.Data.Json.Regions.JsonRegion? GetRegionById(int regionId) => null;
        public Moongate.UO.Data.Maps.MapSector? GetSectorByLocation(int mapId, Point3D location) => null;
        public SectorSystemStats GetStats() => new();
        public int GetUpdateBroadcastSectorRadius() => 0;
        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }
        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }
        public void RemoveEntity(Serial entityId) { }
        public Task HandleAsync(Moongate.Server.Data.Events.Spatial.MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task HandleAsync(Moongate.Server.Data.Events.Characters.PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task HandleAsync(Moongate.Server.Data.Events.Items.DropItemToGroundEvent gameEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DyeColorServiceTestCharacterService : ICharacterService
    {
        public UOMobileEntity? Character { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);
        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue) => Task.CompletedTask;
        public Task<Serial> CreateCharacterAsync(UOMobileEntity character) => Task.FromResult(character.Id);
        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character) => Task.FromResult<UOItemEntity?>(null);
        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character) => Task.FromResult<UOItemEntity?>(null);
        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId) => Task.FromResult(Character?.Id == characterId ? Character : null);
        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId) => Task.FromResult(new List<UOMobileEntity>());
        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId) => Task.FromResult(false);
    }

    [Test]
    public async Task BeginAsync_WhenTargetAccepted_ShouldOpenDyeWindow()
    {
        var service = CreateService(out var itemService, out var targetService, out var sessionService, out var queue, out _, out _);
        var session = AddSession(sessionService);
        session.CharacterId = (Serial)0x40000099u;

        var dyeTub = new UOItemEntity { Id = (Serial)0x40000001u };
        var target = new UOItemEntity
        {
            Id = (Serial)0x40000002u,
            MapId = 0,
            Location = Point3D.Zero,
            EquippedMobileId = session.CharacterId,
            EquippedLayer = ItemLayerType.Shoes
        };
        target.SetCustomBoolean("dyeable", true);
        itemService.Items[dyeTub.Id] = dyeTub;
        itemService.Items[target.Id] = target;

        var opened = await service.BeginAsync(session.SessionId, dyeTub.Id);
        targetService.PendingCallback!(new(new TargetCursorCommandsPacket
        {
            CursorId = (Serial)0x4000ABCDu,
            ClickedOnId = target.Id,
            CursorTarget = TargetCursorSelectionType.SelectObject
        }));

        Assert.Multiple(
            () =>
            {
                Assert.That(opened, Is.True);
                Assert.That(queue.CurrentQueueDepth, Is.EqualTo(1));
            }
        );

        queue.TryDequeue(out var outgoing);
        Assert.That(outgoing.Packet, Is.TypeOf<DisplayDyeWindowPacket>());
    }

    [Test]
    public async Task HandleResponseAsync_WhenPendingRequestExists_ShouldApplyHueAndBroadcastUpdate()
    {
        var service = CreateService(out var itemService, out var targetService, out var sessionService, out var queue, out var spatial, out var characterService);
        var session = AddSession(sessionService);
        session.CharacterId = (Serial)0x40000099u;
        characterService.Character = new UOMobileEntity
        {
            Id = session.CharacterId,
            MapId = 1,
            Location = new(100, 200, 0)
        };

        var dyeTub = new UOItemEntity { Id = (Serial)0x40000001u };
        var target = new UOItemEntity
        {
            Id = (Serial)0x40000002u,
            MapId = 1,
            Location = new(100, 200, 0),
            Hue = 0,
            EquippedMobileId = session.CharacterId,
            EquippedLayer = ItemLayerType.Shirt
        };
        target.SetCustomBoolean("dyeable", true);
        itemService.Items[dyeTub.Id] = dyeTub;
        itemService.Items[target.Id] = target;

        await service.BeginAsync(session.SessionId, dyeTub.Id);
        targetService.PendingCallback!(new(new TargetCursorCommandsPacket
        {
            CursorId = (Serial)0x4000ABCDu,
            ClickedOnId = target.Id,
            CursorTarget = TargetCursorSelectionType.SelectObject
        }));
        queue.TryDequeue(out _);

        var handled = await service.HandleResponseAsync(session, new DyeWindowPacket
        {
            TargetSerial = (uint)target.Id,
            Model = 0x0FAB,
            Hue = 0x9456
        });

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(target.Hue, Is.EqualTo(0x1456));
                Assert.That(itemService.LastUpsertedItem, Is.SameAs(target));
                Assert.That(spatial.LastUpdatedItem, Is.SameAs(target));
                Assert.That(spatial.LastBroadcastPacket, Is.TypeOf<WornItemPacket>());
            }
        );
    }

    private static DyeColorService CreateService(
        out DyeColorServiceTestItemService itemService,
        out DyeColorServiceTestPlayerTargetService targetService,
        out GameNetworkSessionService sessionService,
        out BasePacketListenerTestOutgoingPacketQueue queue,
        out DyeColorServiceTestSpatialWorldService spatialWorldService,
        out DyeColorServiceTestCharacterService characterService
    )
    {
        itemService = new();
        targetService = new();
        sessionService = new();
        queue = new();
        spatialWorldService = new();
        characterService = new();

        return new DyeColorService(targetService, itemService, sessionService, queue, spatialWorldService, characterService);
    }

    private static GameSession AddSession(GameNetworkSessionService sessionService)
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client));
        sessionService.GetOrCreate(client);
        session = sessionService.GetOrCreate(client);

        return session;
    }
}
