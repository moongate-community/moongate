using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.System;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
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

public sealed class ModNameCommandTests
{
    private sealed class ModNameCommandTestGameEventBusService : IGameEventBusService
    {
        public TargetRequestCursorEvent? LastTargetRequestEvent { get; private set; }

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;

            if (gameEvent is TargetRequestCursorEvent targetRequestCursorEvent)
            {
                LastTargetRequestEvent = targetRequestCursorEvent;
            }

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;

        public void TriggerCursorCallback(
            Serial clickedOnId,
            TargetCursorType cursorType = TargetCursorType.Neutral
        )
        {
            if (LastTargetRequestEvent is null)
            {
                throw new InvalidOperationException("No target cursor event was published.");
            }

            var packet = new TargetCursorCommandsPacket
            {
                CursorTarget = TargetCursorSelectionType.SelectObject,
                CursorType = cursorType,
                ClickedOnId = clickedOnId
            };

            LastTargetRequestEvent.Value.Callback(new(packet));
        }
    }

    private sealed class ModNameCommandTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public int UpsertCalls { get; private set; }

        public void SetItem(UOItemEntity item)
            => _items[item.Id] = item;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromResult((Serial)0x40000100u);

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(true);

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromResult<DropItemToGroundResult?>(null);

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromResult(true);

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? item : null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult(
                _items.TryGetValue(itemId, out var item)
                    ? (true, item)
                    : (false, (UOItemEntity?)null)
            );

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;
            UpsertCalls++;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class ModNameCommandTestMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];

        public int CreateOrUpdateCalls { get; private set; }

        public void SetMobile(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles[mobile.Id] = mobile;
            CreateOrUpdateCalls++;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(_mobiles.TryGetValue(id, out var mobile) ? mobile : null);

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
            => Task.FromResult(new UOMobileEntity());

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    private sealed class ModNameCommandTestSpatialWorldService : ISpatialWorldService
    {
        public int AddOrUpdateItemCalls { get; private set; }
        public int AddOrUpdateMobileCalls { get; private set; }
        public List<(long SessionId, IGameNetworkPacket Packet)> BroadcastPackets { get; } = [];
        public List<GameSession> PlayersInRange { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
            AddOrUpdateItemCalls++;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
        {
            _ = mobile;
            AddOrUpdateMobileCalls++;
        }

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = mapId;
            _ = location;
            _ = range;

            foreach (var player in PlayersInRange)
            {
                if (excludeSessionId == player.SessionId)
                {
                    continue;
                }

                BroadcastPackets.Add((player.SessionId, packet));
            }

            return Task.FromResult(BroadcastPackets.Count);
        }

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
            => PlayersInRange
                .Where(player => excludeSession is null || player.SessionId != excludeSession.SessionId)
                .ToList();

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

    private sealed class ModNameCommandTestOutgoingPacketQueue : IOutgoingPacketQueue
    {
        private readonly Queue<OutgoingGamePacket> _items = new();

        public int CurrentQueueDepth => _items.Count;

        public void Enqueue(long sessionId, IGameNetworkPacket packet)
            => _items.Enqueue(new(sessionId, packet, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        public bool TryDequeue(out OutgoingGamePacket gamePacket)
        {
            if (_items.Count == 0)
            {
                gamePacket = default;

                return false;
            }

            gamePacket = _items.Dequeue();

            return true;
        }
    }

    private sealed class ModNameCommandTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public ModNameCommandTestGameNetworkSessionService(params GameSession[] sessions)
        {
            foreach (var session in sessions)
            {
                _sessions[session.SessionId] = session;
            }
        }

        public int Count => _sessions.Count;

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

    [Test]
    public async Task ExecuteCommandAsync_WhenNameMissing_ShouldPrintUsage()
    {
        var command = CreateCommand(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            Array.Empty<GameSession>()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "mod_name",
            [],
            CommandSourceType.InGame,
            1,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .mod_name <new name>"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenItemIsTargeted_ShouldRenamePersistAndRefresh()
    {
        var targetSerial = (Serial)0x40000123u;
        var item = new UOItemEntity
        {
            Id = targetSerial,
            Name = "Old Name",
            ItemId = 0x0F7A,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000011u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000011u,
                Name = "GM",
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        var command = CreateCommand(
            out var gameEventBus,
            out var itemService,
            out _,
            out var spatialWorldService,
            out var outgoingPacketQueue,
            out _,
            [session]
        );
        itemService.SetItem(item);
        spatialWorldService.PlayersInRange.Add(session);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "mod_name New Fancy Name",
            ["New", "Fancy", "Name"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback(targetSerial);

        Assert.Multiple(() =>
        {
            Assert.That(item.Name, Is.EqualTo("New Fancy Name"));
            Assert.That(itemService.UpsertCalls, Is.EqualTo(1));
            Assert.That(spatialWorldService.AddOrUpdateItemCalls, Is.EqualTo(1));
            Assert.That(
                spatialWorldService.BroadcastPackets.Any(packet => packet.Packet is ObjectPropertyList),
                Is.True
            );
            Assert.That(output[^1], Is.EqualTo("Item 1073742115 renamed to 'New Fancy Name'."));
        });
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenMobileIsTargeted_ShouldRenamePersistAndRefresh()
    {
        var targetSerial = (Serial)0x00000045u;
        var mobile = new UOMobileEntity
        {
            Id = targetSerial,
            Name = "Old Mobile",
            MapId = 0,
            Location = new(120, 120, 0)
        };
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000011u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000011u,
                Name = "GM",
                MapId = 0,
                Location = new(120, 120, 0)
            }
        };
        var command = CreateCommand(
            out var gameEventBus,
            out _,
            out var mobileService,
            out var spatialWorldService,
            out var outgoingPacketQueue,
            out _,
            [session]
        );
        mobileService.SetMobile(mobile);
        spatialWorldService.PlayersInRange.Add(session);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "mod_name The Renamed Mob",
            ["The", "Renamed", "Mob"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback(targetSerial);

        Assert.Multiple(() =>
        {
            Assert.That(mobile.Name, Is.EqualTo("The Renamed Mob"));
            Assert.That(mobileService.CreateOrUpdateCalls, Is.EqualTo(1));
            Assert.That(spatialWorldService.AddOrUpdateMobileCalls, Is.EqualTo(1));
            Assert.That(
                spatialWorldService.BroadcastPackets.Any(packet => packet.Packet is ObjectPropertyList),
                Is.True
            );
            Assert.That(outgoingPacketQueue.CurrentQueueDepth, Is.EqualTo(1));
            Assert.That(output[^1], Is.EqualTo("Mobile 69 renamed to 'The Renamed Mob'."));
        });
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetIsInvalid_ShouldPrintError()
    {
        var command = CreateCommand(
            out var gameEventBus,
            out _,
            out _,
            out _,
            out _,
            out _,
            Array.Empty<GameSession>()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "mod_name Renamed",
            ["Renamed"],
            CommandSourceType.InGame,
            1,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback((Serial)0x90000001u);

        Assert.That(output[^1], Is.EqualTo("Selected target is not a valid item or mobile."));
    }

    private static ModNameCommand CreateCommand(
        out ModNameCommandTestGameEventBusService gameEventBusService,
        out ModNameCommandTestItemService itemService,
        out ModNameCommandTestMobileService mobileService,
        out ModNameCommandTestSpatialWorldService spatialWorldService,
        out ModNameCommandTestOutgoingPacketQueue outgoingPacketQueue,
        out ModNameCommandTestGameNetworkSessionService gameNetworkSessionService,
        IReadOnlyList<GameSession> sessions
    )
    {
        gameEventBusService = new();
        itemService = new();
        mobileService = new();
        spatialWorldService = new();
        outgoingPacketQueue = new();
        gameNetworkSessionService = new(sessions.ToArray());

        return new(
            gameEventBusService,
            itemService,
            mobileService,
            spatialWorldService,
            outgoingPacketQueue,
            gameNetworkSessionService
        );
    }
}
