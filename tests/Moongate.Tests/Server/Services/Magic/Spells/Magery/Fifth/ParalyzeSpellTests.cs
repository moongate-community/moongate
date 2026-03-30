using System.Globalization;
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
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Spells.Magery.Fifth;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Fifth;

[TestFixture]
public sealed class ParalyzeSpellTests
{
    [Test]
    public void Info_UsesParalyzeMetadata()
    {
        var spell = new ParalyzeSpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(SpellIds.Magery.Fifth.Paralyze));
                Assert.That(spell.Targeting, Is.EqualTo(SpellTargetingType.RequiredMobile));
                Assert.That(spell.Info.Name, Is.EqualTo("Paralyze"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("An Ex Por"));
                Assert.That(spell.Info.Reagents, Is.EqualTo(new[] { ReagentType.Garlic, ReagentType.MandrakeRoot, ReagentType.SpidersSilk }));
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1, 1 }));
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenTargetIsAlive_AppliesParalyzeStateAndRegistersTimer()
    {
        var spell = new ParalyzeSpell();
        var timerService = new RecordingTimerService();
        var eventBus = new RecordingGameEventBusService();
        var caster = CreateMobile((Serial)0x00000001u);
        var target = CreateMobile((Serial)0x00000002u);
        var context = CreateContext(caster, target, timerService, eventBus);

        await spell.ApplyEffectAsync(context);

        Assert.Multiple(
            () =>
            {
                Assert.That(target.IsParalyzed, Is.True);
                Assert.That(ParalyzeStateHelper.TryGetExpiresAtUtc(target, out _), Is.True);
                Assert.That(ParalyzeStateHelper.TryGetTimerId(target, out var timerId), Is.True);
                Assert.That(timerId, Is.EqualTo("spell_paralyze_0x00000002:1"));
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.RegisteredTimers[0].Interval, Is.EqualTo(TimeSpan.FromSeconds(7)));
                Assert.That(eventBus.Events.OfType<MobilePlayEffectEvent>().Single().ItemId, Is.EqualTo(EffectsUtils.Paralyze));
                Assert.That(eventBus.Events.OfType<MobilePlaySoundEvent>().Single().SoundModel, Is.EqualTo((ushort)0x204));
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenTimerExpires_ClearsParalyzeState()
    {
        var spell = new ParalyzeSpell();
        var timerService = new RecordingTimerService();
        var eventBus = new RecordingGameEventBusService();
        var caster = CreateMobile((Serial)0x00000001u);
        var target = CreateMobile((Serial)0x00000002u);
        var context = CreateContext(caster, target, timerService, eventBus);

        await spell.ApplyEffectAsync(context);
        target.SetCustomString(
            ParalyzeStateHelper.ExpiresAtUtcKey,
            DateTime.UtcNow.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture)
        );
        timerService.RegisteredTimers[0].Callback();

        Assert.Multiple(
            () =>
            {
                Assert.That(target.IsParalyzed, Is.False);
                Assert.That(target.TryGetCustomString(ParalyzeStateHelper.ExpiresAtUtcKey, out _), Is.False);
                Assert.That(target.TryGetCustomString(ParalyzeStateHelper.TimerIdKey, out _), Is.False);
            }
        );
    }

    [Test]
    public async Task ApplyEffectAsync_WhenRecasted_ReplacesPriorTimerWithoutAllowingStaleCallbackToClearState()
    {
        var spell = new ParalyzeSpell();
        var timerService = new RecordingTimerService();
        var eventBus = new RecordingGameEventBusService();
        var caster = CreateMobile((Serial)0x00000001u);
        var target = CreateMobile((Serial)0x00000002u);
        var context = CreateContext(caster, target, timerService, eventBus);

        await spell.ApplyEffectAsync(context);
        var firstCallback = timerService.RegisteredTimers[0].Callback;

        await spell.ApplyEffectAsync(context);

        Assert.That(timerService.UnregisteredTimerIds, Does.Contain("spell_paralyze_0x00000002:1"));

        target.SetCustomString(
            ParalyzeStateHelper.ExpiresAtUtcKey,
            DateTime.UtcNow.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture)
        );

        firstCallback();

        Assert.Multiple(() =>
        {
            Assert.That(target.IsParalyzed, Is.True);
            Assert.That(target.TryGetCustomString(ParalyzeStateHelper.TimerIdKey, out var currentTimerId), Is.True);
            Assert.That(currentTimerId, Is.EqualTo("spell_paralyze_0x00000002:2"));
        });

        timerService.RegisteredTimers[1].Callback();

        Assert.That(target.IsParalyzed, Is.False);
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

    private static UOMobileEntity CreateMobile(Serial id)
    {
        return new UOMobileEntity
        {
            Id = id,
            IsAlive = true,
            MapId = 1,
            Location = new Point3D(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
    }

    private sealed class RecordingTimerService : ITimerService
    {
        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public List<string> UnregisteredTimerIds { get; } = [];

        public void ProcessTick()
        {
        }

        public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
        {
            var timerId = $"{name}:{RegisteredTimers.Count + 1}";
            RegisteredTimers.Add(new(timerId, name, interval, callback, delay, repeat));

            return timerId;
        }

        public void UnregisterAllTimers()
        {
            RegisteredTimers.Clear();
        }

        public bool UnregisterTimer(string timerId)
        {
            UnregisteredTimerIds.Add(timerId);

            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            UnregisteredTimerIds.Add(name);

            return 1;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    private sealed record RegisteredTimer(
        string Id,
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
