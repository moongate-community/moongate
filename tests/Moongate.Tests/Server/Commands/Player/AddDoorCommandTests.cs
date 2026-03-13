using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class AddDoorCommandTests
{
    private sealed class AddDoorTestGameEventBusService : IGameEventBusService
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

    private sealed class AddDoorTestItemFactoryService : IItemFactoryService
    {
        private uint _nextId = 0x40000100;

        public string? LastRequestedTemplateId { get; private set; }

        public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
        {
            LastRequestedTemplateId = itemTemplateId;

            return new UOItemEntity
            {
                Id = (Serial)_nextId++,
                ItemId = itemTemplateId switch
                {
                    "metal_door" => 0x0675,
                    "light_wood_door" => 0x06D5,
                    _ => throw new InvalidOperationException($"Unexpected template '{itemTemplateId}'.")
                },
                Name = itemTemplateId,
                ScriptId = "items.door"
            };
        }

        public UOItemEntity GetNewBackpack()
            => throw new NotSupportedException();

        public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
        {
            definition = new ItemTemplateDefinition { Id = itemTemplateId };

            return itemTemplateId is "metal_door" or "light_wood_door";
        }
    }

    private sealed class AddDoorTestItemService : IItemService
    {
        public UOItemEntity? LastCreatedItem { get; private set; }

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
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromResult(new UOItemEntity());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
            => Task.CompletedTask;

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;
    }

    private sealed class AddDoorTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public AddDoorTestGameNetworkSessionService(params GameSession[] sessions)
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

    private sealed class AddDoorTestCharacterService : ICharacterService
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

    private sealed class AddDoorTestMovementTileQueryService : IMovementTileQueryService
    {
        private readonly Dictionary<(int X, int Y), List<StaticTile>> _staticTiles = [];

        public void SetStaticTile(int x, int y, ushort id, sbyte z = 0)
        {
            var key = (x, y);

            if (!_staticTiles.TryGetValue(key, out var tiles))
            {
                tiles = [];
                _staticTiles[key] = tiles;
            }

            tiles.Add(new StaticTile(id, z));
        }

        public IReadOnlyList<StaticTile> GetStaticTiles(int mapId, int x, int y)
        {
            _ = mapId;

            return _staticTiles.TryGetValue((x, y), out var tiles) ? tiles : [];
        }

        public bool TryGetLandTile(int mapId, int x, int y, out LandTile landTile)
        {
            _ = mapId;
            _ = x;
            _ = y;
            landTile = default;

            return false;
        }

        public bool TryGetMapBounds(int mapId, out int width, out int height)
        {
            _ = mapId;
            width = 6144;
            height = 4096;

            return true;
        }
    }

    private sealed class AddDoorTestSpatialWorldService : ISpatialWorldService
    {
        public int AddOrUpdateItemCalls { get; private set; }
        public UOItemEntity? LastItem { get; private set; }
        public List<UOItemEntity> NearbyItems { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = mapId;
            LastItem = item;
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
            => NearbyItems;

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [];

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
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
    public async Task ExecuteCommandAsync_WhenTypeArgumentIsInvalid_ShouldPrintUsage()
    {
        var command = new AddDoorCommand(
            new AddDoorTestGameEventBusService(),
            new AddDoorTestItemFactoryService(),
            new AddDoorTestItemService(),
            new AddDoorTestSpatialWorldService(),
            new AddDoorTestGameNetworkSessionService(),
            new AddDoorTestCharacterService(),
            new AddDoorTestMovementTileQueryService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_door stone",
            ["stone"],
            CommandSourceType.InGame,
            7,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .add_door [wood|metal]"));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenCalledWithoutArguments_ShouldRequestTargetCursor()
    {
        var gameEventBus = new AddDoorTestGameEventBusService();
        var command = new AddDoorCommand(
            gameEventBus,
            new AddDoorTestItemFactoryService(),
            new AddDoorTestItemService(),
            new AddDoorTestSpatialWorldService(),
            new AddDoorTestGameNetworkSessionService(),
            new AddDoorTestCharacterService(),
            new AddDoorTestMovementTileQueryService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_door",
            [],
            CommandSourceType.InGame,
            7,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(gameEventBus.LastTargetRequestEvent, Is.Not.Null);
        Assert.That(gameEventBus.LastTargetRequestEvent!.Value.SelectionType, Is.EqualTo(TargetCursorSelectionType.SelectLocation));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenWestWallIsDetected_ShouldSpawnWoodDoorWithWestFacing()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000010u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000010u,
                MapId = 1,
                Location = new Point3D(100, 100, 0)
            }
        };
        var gameEventBus = new AddDoorTestGameEventBusService();
        var itemFactoryService = new AddDoorTestItemFactoryService();
        var itemService = new AddDoorTestItemService();
        var spatialWorldService = new AddDoorTestSpatialWorldService();
        var tileQueryService = new AddDoorTestMovementTileQueryService();
        TileData.MaxItemValue = 0xFFFF;
        TileData.ItemTable[0x0001] = new ItemData(string.Empty, UOTileFlag.Wall, 0, 0, 0, 0, 0, 0);
        tileQueryService.SetStaticTile(99, 100, 0x0001, 0);
        var command = new AddDoorCommand(
            gameEventBus,
            itemFactoryService,
            itemService,
            spatialWorldService,
            new AddDoorTestGameNetworkSessionService(session),
            new AddDoorTestCharacterService(),
            tileQueryService
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_door",
            [],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerLocationCallback(new Point3D(100, 100, 0));

        Assert.Multiple(
            () =>
            {
                Assert.That(itemFactoryService.LastRequestedTemplateId, Is.EqualTo("light_wood_door"));
                Assert.That(itemService.LastCreatedItem, Is.Not.Null);
                Assert.That(itemService.LastCreatedItem!.ItemId, Is.EqualTo(DoorGenerationFacing.WestCW.ToItemId(0x06D5)));
                Assert.That(itemService.LastCreatedItem.Direction, Is.EqualTo(DoorGenerationFacing.WestCW.ToDirectionType()));
                Assert.That(itemService.LastCreatedItem.MapId, Is.EqualTo(1));
                Assert.That(itemService.LastCreatedItem.Location, Is.EqualTo(new Point3D(100, 100, 0)));
                Assert.That(itemService.LastCreatedItem.TryGetCustomString(ItemCustomParamKeys.Door.Facing, out var facing), Is.True);
                Assert.That(facing, Is.EqualTo(DoorGenerationFacing.WestCW.ToString()));
                Assert.That(spatialWorldService.AddOrUpdateItemCalls, Is.EqualTo(1));
                Assert.That(output[^1], Does.StartWith("Door 'wood' spawned"));
            }
        );
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenMetalTypeAndSouthWallIsDetected_ShouldSpawnSouthFacingMetalDoor()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var client = new MoongateTCPClient(socket);
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000020u,
            Character = new UOMobileEntity
            {
                Id = (Serial)0x00000020u,
                MapId = 4,
                Location = new Point3D(250, 250, 0)
            }
        };
        var gameEventBus = new AddDoorTestGameEventBusService();
        var itemFactoryService = new AddDoorTestItemFactoryService();
        var itemService = new AddDoorTestItemService();
        var spatialWorldService = new AddDoorTestSpatialWorldService();
        var tileQueryService = new AddDoorTestMovementTileQueryService();
        TileData.MaxItemValue = 0xFFFF;
        TileData.ItemTable[0x0675] = new ItemData(string.Empty, UOTileFlag.Door, 0, 0, 0, 0, 0, 0);
        spatialWorldService.NearbyItems.Add(
            new UOItemEntity
            {
                Id = (Serial)0x40000050u,
                ItemId = 0x0675,
                MapId = 4,
                Location = new Point3D(250, 251, 0)
            }
        );
        var command = new AddDoorCommand(
            gameEventBus,
            itemFactoryService,
            itemService,
            spatialWorldService,
            new AddDoorTestGameNetworkSessionService(session),
            new AddDoorTestCharacterService(),
            tileQueryService
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "add_door metal",
            ["metal"],
            CommandSourceType.InGame,
            session.SessionId,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerLocationCallback(new Point3D(250, 250, 0));

        Assert.Multiple(
            () =>
            {
                Assert.That(itemFactoryService.LastRequestedTemplateId, Is.EqualTo("metal_door"));
                Assert.That(itemService.LastCreatedItem, Is.Not.Null);
                Assert.That(itemService.LastCreatedItem!.ItemId, Is.EqualTo(DoorGenerationFacing.SouthCW.ToItemId(0x0675)));
                Assert.That(itemService.LastCreatedItem.Direction, Is.EqualTo(DirectionType.South));
                Assert.That(output[^1], Does.StartWith("Door 'metal' spawned"));
            }
        );
    }
}
