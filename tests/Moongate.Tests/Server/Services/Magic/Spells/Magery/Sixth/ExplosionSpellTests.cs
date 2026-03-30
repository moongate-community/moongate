using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Magic;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Magic.Spells.Magery.Sixth;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Sixth;

[TestFixture]
public sealed class ExplosionSpellTests
{
    [Test]
    public void Info_UsesExplosionMetadata()
    {
        var spell = new ExplosionSpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(SpellIds.Magery.Sixth.Explosion));
                Assert.That(spell.Targeting, Is.EqualTo(SpellTargetingType.RequiredMobile));
                Assert.That(spell.Info.Name, Is.EqualTo("Explosion"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("Vas Ort Flam"));
                Assert.That(spell.Info.Reagents, Is.EqualTo(new[] { ReagentType.Bloodmoss, ReagentType.MandrakeRoot }));
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1 }));
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenTargetIsAlive_RegistersDelayedDamageWithoutImmediateHit()
    {
        var spell = new ExplosionSpell();
        var timerService = new RecordingTimerService();
        var eventBus = new RecordingGameEventBusService();
        var caster = CreateMobile((Serial)0x00000001u, 50);
        var target = CreateMobile((Serial)0x00000002u, 60);
        var context = CreateContext(caster, target, timerService, eventBus);

        await spell.ApplyEffectAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(target.Hits, Is.EqualTo(60));
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.RegisteredTimers[0].Interval, Is.EqualTo(TimeSpan.FromSeconds(3)));
                Assert.That(eventBus.Events, Is.Empty);
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenTimerExpires_DealsDamageAndPublishesEffectAndSound()
    {
        var spell = new ExplosionSpell();
        var timerService = new RecordingTimerService();
        var eventBus = new RecordingGameEventBusService();
        var caster = CreateMobile((Serial)0x00000001u, 50);
        var target = CreateMobile((Serial)0x00000002u, 60);
        var context = CreateContext(caster, target, timerService, eventBus);

        await spell.ApplyEffectAsync(context);
        timerService.RegisteredTimers[0].Callback();

        Assert.Multiple(
            () =>
            {
                Assert.That(target.Hits, Is.InRange(16, 37));
                Assert.That(eventBus.Events.OfType<MobilePlayEffectEvent>().Single().ItemId, Is.EqualTo(EffectsUtils.Explosion));
                Assert.That(eventBus.Events.OfType<MobilePlaySoundEvent>().Single().SoundModel, Is.EqualTo((ushort)0x307));
            }
        );
    }

    private static SpellExecutionContext CreateContext(
        UOMobileEntity caster,
        UOMobileEntity target,
        RecordingTimerService timerService,
        RecordingGameEventBusService eventBus
    )
    {
        return new SpellExecutionContext(
            caster,
            SpellTargetData.Mobile(target.Id),
            target,
            null,
            new NullSpatialWorldService(),
            eventBus,
            timerService,
            new NullItemService()
        );
    }

    private static UOMobileEntity CreateMobile(Serial id, int hits)
    {
        return new UOMobileEntity
        {
            Id = id,
            IsAlive = true,
            MapId = 1,
            Location = new Point3D(100, 100, 0),
            Hits = hits,
            MaxHits = hits
        };
    }

    private sealed class RecordingTimerService : ITimerService
    {
        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public void ProcessTick()
        {
        }

        public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
        {
            RegisteredTimers.Add(new(name, interval, callback, delay, repeat));

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

    private sealed class RecordingGameEventBusService : IGameEventBusService
    {
        public List<IGameEvent> Events { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            Events.Add(gameEvent);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener)
            where TEvent : IGameEvent
        {
            _ = listener;
        }
    }

    private sealed class NullSpatialWorldService : ISpatialWorldService
    {
        public void AddOrUpdateItem(UOItemEntity item, int mapId) => throw new NotSupportedException();
        public void AddOrUpdateMobile(UOMobileEntity mobile) => throw new NotSupportedException();
        public void AddRegion(JsonRegion region) => throw new NotSupportedException();
        public Task<int> BroadcastToPlayersAsync(IGameNetworkPacket packet, int mapId, Point3D location, int? range = null, long? excludeSessionId = null) => Task.FromResult(0);
        public List<MapSector> GetActiveSectors() => [];
        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2) => [];
        public int GetMusic(int mapId, Point3D location) => 0;
        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId) => [];
        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId) => [];
        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null) => [];
        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY) => [];
        public JsonRegion? GetRegionById(int regionId) => null;
        public MapSector? GetSectorByLocation(int mapId, Point3D location) => null;
        public SectorSystemStats GetStats() => new();
        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) => throw new NotSupportedException();
        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) => throw new NotSupportedException();
        public void RemoveEntity(Serial serial) => throw new NotSupportedException();
    }

    private sealed class NullItemService : IItemService
    {
        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items) => throw new NotSupportedException();
        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true) => throw new NotSupportedException();
        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true) => throw new NotSupportedException();
        public Task<Serial> CreateItemAsync(UOItemEntity item) => throw new NotSupportedException();
        public Task<bool> DeleteItemAsync(Serial itemId) => throw new NotSupportedException();
        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0) => throw new NotSupportedException();
        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, Moongate.UO.Data.Types.ItemLayerType layer) => throw new NotSupportedException();
        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY) => throw new NotSupportedException();
        public Task<UOItemEntity?> GetItemAsync(Serial itemId) => throw new NotSupportedException();
        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId) => throw new NotSupportedException();
        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0) => throw new NotSupportedException();
        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0) => throw new NotSupportedException();
        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId) => throw new NotSupportedException();
        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId) => throw new NotSupportedException();
        public Task UpsertItemAsync(UOItemEntity item) => throw new NotSupportedException();
        public Task UpsertItemsAsync(params UOItemEntity[] items) => throw new NotSupportedException();
    }
}
