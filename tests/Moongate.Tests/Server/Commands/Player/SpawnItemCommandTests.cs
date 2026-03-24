using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class SpawnItemCommandTests
{
    private sealed class SpawnItemTestGameEventBusService : IGameEventBusService
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

        public void TriggerLocationCallback(Point3D location)
        {
            if (LastTargetRequestEvent is null)
            {
                throw new InvalidOperationException("No target cursor event was published.");
            }

            var packet = new TargetCursorCommandsPacket
            {
                CursorTarget = TargetCursorSelectionType.SelectLocation,
                CursorType = TargetCursorType.Helpful,
                Location = location
            };

            LastTargetRequestEvent.Value.Callback(new(packet));
        }
    }

    private sealed class SpawnItemTestItemFactoryService : IItemFactoryService
    {
        private uint _nextId = 0x40000100;
        private readonly HashSet<string> _knownTemplateIds = ["brick", "dagger"];

        public string? LastRequestedTemplateId { get; private set; }

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            LastRequestedTemplateId = itemTemplateId;

            return new()
            {
                Id = (Serial)_nextId++,
                Name = itemTemplateId,
                ItemId = 0x1F1C,
                ScriptId = "items.brick"
            };
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            if (_knownTemplateIds.Contains(itemTemplateId))
            {
                definition = new() { Id = itemTemplateId };

                return true;
            }

            definition = null;

            return false;
        }
    }

    private sealed class SpawnItemTestItemService : IItemService
    {
        public UOItemEntity? LastCreatedItem { get; private set; }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => item;

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            LastCreatedItem = item;

            return Task.FromResult(item.Id);
        }

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
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(false);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(false);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class SpawnItemTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public SpawnItemTestGameNetworkSessionService(params GameSession[] sessions)
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

    private sealed class SpawnItemTestCharacterService : ICharacterService
    {
        public UOMobileEntity? CharacterToReturn { get; set; }

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
            => Task.CompletedTask;

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => Task.FromResult((Serial)0x00000003u);

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
            => Task.FromResult<UOItemEntity?>(null);

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(CharacterToReturn);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => Task.FromResult(true);
    }

    private sealed class SpawnItemTestSpatialWorldService : ISpatialWorldService
    {
        public int AddOrUpdateItemCalls { get; private set; }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
            AddOrUpdateItemCalls++;
        }

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
    public async Task ExecuteCommandAsync_WhenArgumentsAreMissing_ShouldPrintUsage()
    {
        var command = new SpawnItemCommand(
            new SpawnItemTestGameEventBusService(),
            new SpawnItemTestItemFactoryService(),
            new SpawnItemTestItemService(),
            new SpawnItemTestSpatialWorldService(),
            new SpawnItemTestGameNetworkSessionService(),
            new SpawnItemTestCharacterService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            ".spawn_item",
            [],
            CommandSourceType.InGame,
            7,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .spawn_item <templateId>"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenLocationSelected_ShouldCreateItemAtTargetedTile()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000044u,
            Character = new()
            {
                Id = (Serial)0x00000044u,
                MapId = 3,
                Location = new(150, 150, 0)
            }
        };
        var gameEventBus = new SpawnItemTestGameEventBusService();
        var itemFactory = new SpawnItemTestItemFactoryService();
        var itemService = new SpawnItemTestItemService();
        var spatial = new SpawnItemTestSpatialWorldService();
        var command = new SpawnItemCommand(
            gameEventBus,
            itemFactory,
            itemService,
            spatial,
            new SpawnItemTestGameNetworkSessionService(session),
            new SpawnItemTestCharacterService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            ".spawn_item brick",
            ["brick"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerLocationCallback(new(120, 121, 5));

        Assert.Multiple(
            () =>
            {
                Assert.That(itemFactory.LastRequestedTemplateId, Is.EqualTo("brick"));
                Assert.That(itemService.LastCreatedItem, Is.Not.Null);
                Assert.That(itemService.LastCreatedItem!.MapId, Is.EqualTo(3));
                Assert.That(itemService.LastCreatedItem.Location, Is.EqualTo(new Point3D(120, 121, 5)));
                Assert.That(spatial.AddOrUpdateItemCalls, Is.EqualTo(1));
                Assert.That(output[^1], Does.StartWith("Item 'brick' spawned"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTemplateIsUnknown_ShouldPrintError()
    {
        var command = new SpawnItemCommand(
            new SpawnItemTestGameEventBusService(),
            new SpawnItemTestItemFactoryService(),
            new SpawnItemTestItemService(),
            new SpawnItemTestSpatialWorldService(),
            new SpawnItemTestGameNetworkSessionService(),
            new SpawnItemTestCharacterService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            ".spawn_item unknown",
            ["unknown"],
            CommandSourceType.InGame,
            7,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Unknown item template: unknown"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTemplateIsValid_ShouldRequestLocationCursor()
    {
        var gameEventBus = new SpawnItemTestGameEventBusService();
        var command = new SpawnItemCommand(
            gameEventBus,
            new SpawnItemTestItemFactoryService(),
            new SpawnItemTestItemService(),
            new SpawnItemTestSpatialWorldService(),
            new SpawnItemTestGameNetworkSessionService(),
            new SpawnItemTestCharacterService()
        );
        var context = new CommandSystemContext(
            ".spawn_item brick",
            ["brick"],
            CommandSourceType.InGame,
            7,
            (_, _) => { }
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(gameEventBus.LastTargetRequestEvent, Is.Not.Null);
        Assert.That(
            gameEventBus.LastTargetRequestEvent!.Value.SelectionType,
            Is.EqualTo(TargetCursorSelectionType.SelectLocation)
        );
    }
}
