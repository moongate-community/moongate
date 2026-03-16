using Moongate.Network.Packets.Incoming.Targeting;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Commands.Player;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Commands.Player;

public sealed class FlipCommandTests
{
    private sealed class FlipCommandTestGameEventBusService : IGameEventBusService
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

    private sealed class FlipCommandTestItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = [];

        public int UpsertCalls { get; private set; }

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

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
            => Task.FromResult<UOItemEntity?>(null);

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => Task.FromResult(true);

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromResult(true);

        public void SetItem(UOItemEntity item)
            => _items[item.Id] = item;

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
    }

    private sealed class FlipCommandTestSpatialWorldService : ISpatialWorldService
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
    public async Task ExecuteCommandAsync_WhenArgumentsAreInvalid_ShouldPrintUsage()
    {
        var gameEventBus = new FlipCommandTestGameEventBusService();
        var command = new FlipCommand(
            gameEventBus,
            new FlipCommandTestItemService(),
            new FlipCommandTestSpatialWorldService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "flip bad",
            ["bad"],
            CommandSourceType.InGame,
            2,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);

        Assert.That(output[^1], Is.EqualTo("Usage: .flip"));
        Assert.That(gameEventBus.LastTargetRequestEvent, Is.Null);
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetIsNotAnItem_ShouldPrintError()
    {
        var gameEventBus = new FlipCommandTestGameEventBusService();
        var command = new FlipCommand(
            gameEventBus,
            new FlipCommandTestItemService(),
            new FlipCommandTestSpatialWorldService()
        );
        var output = new List<string>();
        var context = new CommandSystemContext(
            "flip",
            [],
            CommandSourceType.InGame,
            2,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback((Serial)0x40001234u);

        Assert.That(output[^1], Is.EqualTo("Selected target is not a valid item."));
    }

    [Test]
    public async Task ExecuteCommandAsync_WhenTargetItemHasFlippableIds_ShouldFlipAndPersist()
    {
        var gameEventBus = new FlipCommandTestGameEventBusService();
        var itemService = new FlipCommandTestItemService();
        var spatialService = new FlipCommandTestSpatialWorldService();
        var item = new UOItemEntity
        {
            Id = (Serial)0x40001000u,
            ItemId = 0x0675,
            MapId = 1,
            Location = new(100, 200, 0)
        };

        item.SetCustomString("flippable_item_ids", "0x0675,0x0676");
        itemService.SetItem(item);

        var command = new FlipCommand(gameEventBus, itemService, spatialService);
        var output = new List<string>();
        var context = new CommandSystemContext(
            "flip",
            [],
            CommandSourceType.InGame,
            2,
            (message, _) => output.Add(message)
        );

        await command.ExecuteCommandAsync(context);
        gameEventBus.TriggerCursorCallback(item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(item.ItemId, Is.EqualTo(0x0676));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(1));
                Assert.That(spatialService.AddOrUpdateItemCalls, Is.EqualTo(1));
                Assert.That(output[^1], Is.EqualTo($"Item {item.Id} flipped to 0x0676."));
            }
        );
    }
}
