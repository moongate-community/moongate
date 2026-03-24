using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class RemoveItemCommandTests
{
    private sealed class RemoveItemCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.Values.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.Values.FirstOrDefault(current => current.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class RemoveItemCommandTestPlayerTargetService : IPlayerTargetService
    {
        public long LastSessionId { get; private set; }
        public TargetCursorSelectionType LastSelectionType { get; private set; }
        public TargetCursorType LastCursorType { get; private set; }
        public Action<PendingCursorCallback>? Callback { get; private set; }

        public Task SendCancelTargetCursorAsync(long sessionId, Serial cursorId)
        {
            _ = sessionId;
            _ = cursorId;

            return Task.CompletedTask;
        }

        public Task<Serial> SendTargetCursorAsync(
            long sessionId,
            Action<PendingCursorCallback> callback,
            TargetCursorSelectionType selectionType = TargetCursorSelectionType.SelectLocation,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            LastSessionId = sessionId;
            LastSelectionType = selectionType;
            LastCursorType = cursorType;
            Callback = callback;

            return Task.FromResult((Serial)0x00004567u);
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    private sealed class RemoveItemCommandTestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> Items { get; } = [];
        public Serial LastDeletedItemId { get; private set; }
        public bool DeleteItemResult { get; } = true;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult(item.Id);

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            LastDeletedItemId = itemId;

            return Task.FromResult(DeleteItemResult);
        }

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
        {
            Items.TryGetValue(itemId, out var item);

            return Task.FromResult(item);
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((Items.ContainsKey(itemId), Items.GetValueOrDefault(itemId)));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class RemoveItemCommandTestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> Mobiles { get; } = [];
        public Serial LastDeletedMobileId { get; private set; }
        public bool DeleteResult { get; } = true;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            LastDeletedMobileId = id;

            return Task.FromResult(DeleteResult);
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            Mobiles.TryGetValue(id, out var mobile);

            return Task.FromResult(mobile);
        }

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

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
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    private sealed class RemoveItemCommandTestSpatialWorldService : ISpatialWorldService
    {
        public List<Serial> RemovedEntities { get; } = [];

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

        public void RemoveEntity(Serial serial)
            => RemovedEntities.Add(serial);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenCalled_ShouldRequestObjectTargetCursor()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client));
        var sessionService = new RemoveItemCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new RemoveItemCommandTestPlayerTargetService();
        var command = new RemoveItemCommand(
            sessionService,
            targetService,
            new RemoveItemCommandTestItemService(),
            new RemoveItemCommandTestMobileService(),
            new RemoveItemCommandTestSpatialWorldService()
        );
        var context = new CommandSystemContext(
            ".remove_item",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(targetService.LastSessionId, Is.EqualTo(session.SessionId));
                Assert.That(targetService.LastSelectionType, Is.EqualTo(TargetCursorSelectionType.SelectObject));
                Assert.That(targetService.LastCursorType, Is.EqualTo(TargetCursorType.Helpful));
                Assert.That(targetService.Callback, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenItemTargetSelected_ShouldDeleteItem()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client));
        var sessionService = new RemoveItemCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new RemoveItemCommandTestPlayerTargetService();
        var itemService = new RemoveItemCommandTestItemService();
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000100u,
            Name = "brick"
        };
        itemService.Items[item.Id] = item;
        var output = new List<string>();
        var command = new RemoveItemCommand(
            sessionService,
            targetService,
            itemService,
            new RemoveItemCommandTestMobileService(),
            new RemoveItemCommandTestSpatialWorldService()
        );
        var context = new CommandSystemContext(
            ".remove_item",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        targetService.Callback!(
            new(
                new()
                {
                    CursorTarget = TargetCursorSelectionType.SelectObject,
                    CursorType = TargetCursorType.Helpful,
                    ClickedOnId = item.Id
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.LastDeletedItemId, Is.EqualTo(item.Id));
                Assert.That(output[^1], Is.EqualTo("Removed item brick."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenNpcTargetSelected_ShouldDeleteNpcAndRemoveFromSpatialWorld()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client));
        var sessionService = new RemoveItemCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new RemoveItemCommandTestPlayerTargetService();
        var mobileService = new RemoveItemCommandTestMobileService();
        var spatial = new RemoveItemCommandTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x00000080u,
            Name = "Zombie",
            IsPlayer = false
        };
        mobileService.Mobiles[npc.Id] = npc;
        var output = new List<string>();
        var command = new RemoveItemCommand(
            sessionService,
            targetService,
            new RemoveItemCommandTestItemService(),
            mobileService,
            spatial
        );
        var context = new CommandSystemContext(
            ".remove_item",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        targetService.Callback!(
            new(
                new()
                {
                    CursorTarget = TargetCursorSelectionType.SelectObject,
                    CursorType = TargetCursorType.Helpful,
                    ClickedOnId = npc.Id
                }
            )
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(mobileService.LastDeletedMobileId, Is.EqualTo(npc.Id));
                Assert.That(spatial.RemovedEntities, Contains.Item(npc.Id));
                Assert.That(output[^1], Is.EqualTo("Removed NPC Zombie."));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenPlayerTargetSelected_ShouldRejectRemoval()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client));
        var sessionService = new RemoveItemCommandTestGameNetworkSessionService();
        sessionService.Add(session);
        var targetService = new RemoveItemCommandTestPlayerTargetService();
        var output = new List<string>();
        var player = new UOMobileEntity
        {
            Id = (Serial)0x00000033u,
            Name = "PlayerOne",
            IsPlayer = true
        };
        var playerSession = new GameSession(new(client))
        {
            CharacterId = player.Id,
            Character = player
        };
        sessionService.Add(playerSession);
        var command = new RemoveItemCommand(
            sessionService,
            targetService,
            new RemoveItemCommandTestItemService(),
            new RemoveItemCommandTestMobileService(),
            new RemoveItemCommandTestSpatialWorldService()
        );
        var context = new CommandSystemContext(
            ".remove_item",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        targetService.Callback!(
            new(
                new()
                {
                    CursorTarget = TargetCursorSelectionType.SelectObject,
                    CursorType = TargetCursorType.Helpful,
                    ClickedOnId = player.Id
                }
            )
        );

        Assert.That(output[^1], Is.EqualTo("Cannot remove player characters."));
    }
}
