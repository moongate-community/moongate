using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Magic.Spells.Magery.Seventh;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Seventh;

[TestFixture]
public sealed class GateTravelSpellTests
{
    [Test]
    public void Info_UsesGateTravelMetadata()
    {
        var spell = new GateTravelSpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(SpellIds.Magery.Seventh.GateTravel));
                Assert.That(spell.Targeting, Is.EqualTo(SpellTargetingType.RequiredItem));
                Assert.That(spell.Info.Name, Is.EqualTo("Gate Travel"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("Vas Rel Por"));
                Assert.That(
                    spell.Info.Reagents,
                    Is.EqualTo(new[] { ReagentType.BlackPearl, ReagentType.MandrakeRoot, ReagentType.SulfurousAsh })
                );
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1, 1 }));
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenRuneIsNotMarked_DoesNotSpawnMoongates()
    {
        var spell = new GateTravelSpell();
        var itemService = new RecordingItemService();
        var spatialWorldService = new RecordingSpatialWorldService();
        var timerService = new RecordingTimerService();
        var caster = CreateMobile((Serial)0x00000001u, 1, new Point3D(100, 100, 0));
        var rune = CreateRune((Serial)0x40000010u);
        var context = CreateContext(caster, rune, itemService, spatialWorldService, timerService);

        await spell.ApplyEffectAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.SpawnedTemplateIds, Is.Empty);
                Assert.That(itemService.Items, Has.Count.EqualTo(1));
                Assert.That(spatialWorldService.AddedItems, Is.Empty);
                Assert.That(timerService.RegisteredTimers, Is.Empty);
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenRuneUsesImportedTargetKeys_SpawnsLinkedTemporaryMoongates()
    {
        var spell = new GateTravelSpell();
        var itemService = new RecordingItemService();
        var spatialWorldService = new RecordingSpatialWorldService();
        var timerService = new RecordingTimerService();
        var caster = CreateMobile((Serial)0x00000001u, 1, new Point3D(100, 100, 0));
        var rune = CreateRune((Serial)0x40000010u);
        rune.SetCustomBoolean("marked", true);
        rune.SetCustomLocation("target", new Point3D(512, 640, 0));
        rune.SetCustomString("target_map", "0");
        rune.SetCustomString("description", "Britain Bank");
        var context = CreateContext(caster, rune, itemService, spatialWorldService, timerService);

        await spell.ApplyEffectAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.SpawnedTemplateIds, Is.EqualTo(new[] { "moongate", "moongate" }));
                Assert.That(spatialWorldService.AddedItems, Has.Count.EqualTo(2));
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
            }
        );

        var firstGate = itemService.SpawnedItems[0];
        var secondGate = itemService.SpawnedItems[1];

        Assert.Multiple(
            () =>
            {
                Assert.That(firstGate.MapId, Is.EqualTo(caster.MapId));
                Assert.That(firstGate.Location, Is.EqualTo(caster.Location));
                Assert.That(firstGate.TryGetCustomLocation("point_dest", out var firstDestination), Is.True);
                Assert.That(firstDestination, Is.EqualTo(new Point3D(512, 640, 0)));
                Assert.That(firstGate.TryGetCustomString("map_dest", out var firstMapDest), Is.True);
                Assert.That(firstMapDest, Is.EqualTo("0"));
                Assert.That(firstGate.TryGetCustomInteger("linked_gate_serial", out var firstLinkedGate), Is.True);
                Assert.That(firstLinkedGate, Is.EqualTo((long)secondGate.Id.Value));
                Assert.That(firstGate.TryGetCustomString("gate_description", out var description), Is.True);
                Assert.That(description, Is.EqualTo("Britain Bank"));
            }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(secondGate.MapId, Is.EqualTo(0));
                Assert.That(secondGate.Location, Is.EqualTo(new Point3D(512, 640, 0)));
                Assert.That(secondGate.TryGetCustomLocation("point_dest", out var secondDestination), Is.True);
                Assert.That(secondDestination, Is.EqualTo(caster.Location));
                Assert.That(secondGate.TryGetCustomString("map_dest", out var secondMapDest), Is.True);
                Assert.That(secondMapDest, Is.EqualTo(caster.MapId.ToString()));
                Assert.That(secondGate.TryGetCustomInteger("linked_gate_serial", out var secondLinkedGate), Is.True);
                Assert.That(secondLinkedGate, Is.EqualTo((long)firstGate.Id.Value));
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenCleanupTimerExpires_RemovesBothMoongates()
    {
        var spell = new GateTravelSpell();
        var itemService = new RecordingItemService();
        var spatialWorldService = new RecordingSpatialWorldService();
        var timerService = new RecordingTimerService();
        var caster = CreateMobile((Serial)0x00000001u, 1, new Point3D(100, 100, 0));
        var rune = CreateRune((Serial)0x40000010u);
        rune.SetCustomBoolean("marked", true);
        rune.SetCustomLocation("point_dest", new Point3D(200, 220, 0));
        rune.SetCustomString("map_dest", "1");
        var context = CreateContext(caster, rune, itemService, spatialWorldService, timerService);

        await spell.ApplyEffectAsync(context);
        timerService.RegisteredTimers[0].Callback();

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.DeletedItemIds, Has.Count.EqualTo(2));
                Assert.That(spatialWorldService.RemovedEntityIds, Has.Count.EqualTo(2));
                Assert.That(itemService.Items, Has.Count.EqualTo(1));
            }
        );
    }

    private static SpellExecutionContext CreateContext(
        UOMobileEntity caster,
        UOItemEntity rune,
        RecordingItemService itemService,
        RecordingSpatialWorldService spatialWorldService,
        RecordingTimerService timerService
    )
    {
        itemService.Add(rune);

        return new SpellExecutionContext(
            caster,
            SpellTargetData.Item(rune.Id, rune.Location, (ushort)rune.ItemId),
            null,
            rune,
            spatialWorldService,
            new NullGameEventBusService(),
            timerService,
            itemService
        );
    }

    private static UOMobileEntity CreateMobile(Serial id, int mapId, Point3D location)
    {
        return new UOMobileEntity
        {
            Id = id,
            IsAlive = true,
            MapId = mapId,
            Location = location,
            Hits = 50,
            MaxHits = 50
        };
    }

    private static UOItemEntity CreateRune(Serial id)
    {
        var rune = new UOItemEntity
        {
            Id = id,
            ItemId = 0x1F14,
            MapId = 1,
            Location = new Point3D(100, 100, 0),
            Name = "Recall Rune"
        };
        rune.SetCustomString("item_template_id", "recall_rune");

        return rune;
    }

    private sealed class RecordingItemService : IItemService
    {
        private uint _nextItemId = 0x40000020u;

        public Dictionary<Serial, UOItemEntity> Items { get; } = [];

        public List<string> SpawnedTemplateIds { get; } = [];

        public List<UOItemEntity> SpawnedItems { get; } = [];

        public List<Serial> DeletedItemIds { get; } = [];

        public void Add(UOItemEntity item)
            => Items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                Items[item.Id] = item;
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
        {
            Items[item.Id] = item;

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedItemIds.Add(itemId);

            return Task.FromResult(Items.Remove(itemId));
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(Items.GetValueOrDefault(itemId));

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
        {
            if (!Items.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(false);
            }

            item.Location = location;
            item.MapId = mapId;
            item.ParentContainerId = Serial.Zero;
            item.EquippedMobileId = Serial.Zero;
            Items[itemId] = item;

            return Task.FromResult(true);
        }

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
        {
            SpawnedTemplateIds.Add(itemTemplateId);

            var item = new UOItemEntity
            {
                Id = (Serial)_nextItemId++,
                ItemId = 0x0F6C,
                Name = "Moongate",
                ScriptId = "items.teleport"
            };
            item.SetCustomString("item_template_id", itemTemplateId);
            Items[item.Id] = item;
            SpawnedItems.Add(item);

            return Task.FromResult(item);
        }

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((Items.ContainsKey(itemId), Items.GetValueOrDefault(itemId)));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            Items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => BulkUpsertItemsAsync(items);
    }

    private sealed class RecordingSpatialWorldService : ISpatialWorldService
    {
        public List<UOItemEntity> AddedItems { get; } = [];

        public List<Serial> RemovedEntityIds { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = mapId;
            AddedItems.Add(item);
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => throw new NotSupportedException();

        public void AddRegion(JsonRegion region)
            => throw new NotSupportedException();

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        ) => Task.FromResult(0);

        public List<MapSector> GetActiveSectors() => [];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2) => [];

        public int GetMusic(int mapId, Point3D location) => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId) => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId) => [];

        public List<Moongate.Server.Data.Session.GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            Moongate.Server.Data.Session.GameSession? excludeSession = null
        ) => [];

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY) => [];

        public JsonRegion? GetRegionById(int regionId) => null;

        public MapSector? GetSectorByLocation(int mapId, Point3D location) => null;

        public SectorSystemStats GetStats() => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
            => throw new NotSupportedException();

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
            => throw new NotSupportedException();

        public void RemoveEntity(Serial serial)
            => RemovedEntityIds.Add(serial);
    }

    private sealed class RecordingTimerService : ITimerService
    {
        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public void ProcessTick()
        {
        }

        public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
        {
            RegisteredTimers.Add(new RegisteredTimer(name, interval, callback, delay, repeat));

            return name;
        }

        public void UnregisterAllTimers()
        {
            RegisteredTimers.Clear();
        }

        public bool UnregisterTimer(string timerId)
        {
            _ = timerId;

            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            _ = name;

            return 0;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    private sealed record RegisteredTimer(
        string Name,
        TimeSpan Interval,
        Action Callback,
        TimeSpan? Delay,
        bool Repeat
    );

    private sealed class NullGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = gameEvent;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
        {
            _ = listener;
        }
    }
}
