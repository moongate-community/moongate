using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class CombatServiceTests
{
    private sealed class TimerServiceSpy : ITimerService
    {
        public sealed record RegisteredTimer(
            string Id,
            string Name,
            TimeSpan Interval,
            TimeSpan? Delay,
            bool Repeat,
            Action Callback
        );

        private int _nextId;

        public List<RegisteredTimer> RegisteredTimers { get; } = [];
        public List<string> UnregisteredNames { get; } = [];

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            var timer = new RegisteredTimer($"timer-{++_nextId}", name, interval, delay, repeat, callback);
            RegisteredTimers.Add(timer);

            return timer.Id;
        }

        public void UnregisterAllTimers()
            => RegisteredTimers.Clear();

        public bool UnregisterTimer(string timerId)
        {
            var removed = RegisteredTimers.RemoveAll(timer => timer.Id == timerId);

            return removed > 0;
        }

        public int UnregisterTimersByName(string name)
        {
            UnregisteredNames.Add(name);
            return RegisteredTimers.RemoveAll(timer => timer.Name == name);
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;
            return 0;
        }
    }

    private sealed class InMemoryMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();

        public void Add(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(_mobiles.Remove(id));
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _mobiles.TryGetValue(id, out var mobile);
            return Task.FromResult(mobile);
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
            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromException<UOMobileEntity>(new NotSupportedException());

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult((false, (UOMobileEntity?)null));
    }

    private sealed class CombatTestSpatialWorldService : ISpatialWorldService
    {
        public JsonRegion? ResolvedRegion { get; set; }
        public List<IGameNetworkPacket> BroadcastPackets { get; } = [];

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
        {
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;
            BroadcastPackets.Add(packet);
            return Task.FromResult(0);
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

        public JsonRegion? ResolveRegion(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;
            return ResolvedRegion;
        }
    }

    private sealed class RecordingGameEventBusService : IGameEventBusService
    {
        public List<object> Events { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            Events.Add(gameEvent!);
            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent
            => _ = listener;
    }

    [Test]
    public async Task TrySetCombatantAsync_ShouldSetCombatantWarmodeAndScheduleTimer()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000003u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        mobileService.Add(attacker);
        mobileService.Add(defender);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = attacker.Id,
            Character = attacker
        };
        sessionService.Add(session);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus
        );

        var result = await service.TrySetCombatantAsync(attacker.Id, defender.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(attacker.CombatantId, Is.EqualTo(defender.Id));
                Assert.That(attacker.Warmode, Is.True);
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.RegisteredTimers[0].Name, Is.EqualTo($"combat:{(uint)attacker.Id}"));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is MobileWarModeChangedEvent), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent.GetType().Name == "CombatStartedEvent"), Is.True);
            }
        );

        Assert.That(outgoingQueue.TryDequeue(out var outgoing), Is.True);
        Assert.That(outgoing.Packet, Is.TypeOf<ChangeCombatantPacket>());
        Assert.That(((ChangeCombatantPacket)outgoing.Packet).CombatantId, Is.EqualTo(defender.Id));
    }

    [Test]
    public async Task ScheduledSwing_WhenTargetInRange_ShouldBroadcastFightPacketAndApplyDamage()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            MinWeaponDamage = 6,
            MaxWeaponDamage = 6
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000003u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 40,
            MaxHits = 40
        };
        mobileService.Add(attacker);
        mobileService.Add(defender);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = attacker.Id,
            Character = attacker
        };
        sessionService.Add(session);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastPackets.Any(packet => packet is FightOccurringPacket), Is.True);
                Assert.That(defender.Hits, Is.EqualTo(34));
                Assert.That(attacker.Aggressed, Has.Count.EqualTo(1));
                Assert.That(defender.Aggressors, Has.Count.EqualTo(1));
                Assert.That(attacker.LastCombatAtUtc, Is.Not.Null);
                Assert.That(defender.LastCombatAtUtc, Is.Not.Null);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent), Is.True);
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.UnregisteredNames, Contains.Item($"combat:{(uint)attacker.Id}"));
            }
        );

        var hitEvent = eventBus.Events.OfType<CombatHitEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(hitEvent.Attacker, Is.SameAs(attacker));
                Assert.That(hitEvent.Defender, Is.SameAs(defender));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenHitRollFails_ShouldPublishMissEventWithoutApplyingDamage()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            RuntimeModifiers = new()
            {
                HitChanceIncrease = -20
            },
            MinWeaponDamage = 6,
            MaxWeaponDamage = 6
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000003u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 40,
            MaxHits = 40,
            RuntimeModifiers = new()
            {
                DefenseChanceIncrease = 25
            }
        };
        mobileService.Add(attacker);
        mobileService.Add(defender);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = attacker.Id,
            Character = attacker
        };
        sessionService.Add(session);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastPackets.Any(packet => packet is FightOccurringPacket), Is.True);
                Assert.That(defender.Hits, Is.EqualTo(40));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatMissEvent), Is.True);
                Assert.That(attacker.LastCombatAtUtc, Is.Not.Null);
                Assert.That(defender.LastCombatAtUtc, Is.Not.Null);
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
            }
        );

        var missEvent = eventBus.Events.OfType<CombatMissEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(missEvent.Attacker, Is.SameAs(attacker));
                Assert.That(missEvent.Defender, Is.SameAs(defender));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenMapDisallowsHarmfulAction_ShouldClearCombatantAndPublishAttemptEvent()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService
        {
            ResolvedRegion = new JsonTownRegion
            {
                Type = "TownRegion",
                Map = "Trammel",
                Name = "Britain",
                GuardsDisabled = false
            }
        };
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            IsPlayer = true,
            MapId = 1,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            Notoriety = Notoriety.Innocent
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000003u,
            IsPlayer = true,
            MapId = 1,
            Location = new(101, 100, 0),
            Hits = 40,
            MaxHits = 40,
            Notoriety = Notoriety.Innocent
        };
        mobileService.Add(attacker);
        mobileService.Add(defender);

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = attacker.Id,
            Character = attacker
        };
        sessionService.Add(session);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(attacker.CombatantId, Is.EqualTo(Serial.Zero));
                Assert.That(defender.Hits, Is.EqualTo(40));
                Assert.That(
                    eventBus.Events.Any(gameEvent => gameEvent.GetType().Name == "CombatAttemptEvent"),
                    Is.True
                );
            }
        );
    }

    private static void EnsureMapsRegistered()
    {
        if (Map.GetMap(0) is null)
        {
            _ = Map.RegisterMap(0, 0, 0, 6144, 4096, SeasonType.Summer, "Felucca", MapRules.FeluccaRules);
        }

        if (Map.GetMap(1) is null)
        {
            _ = Map.RegisterMap(1, 1, 1, 6144, 4096, SeasonType.Summer, "Trammel", MapRules.TrammelRules);
        }
    }
}
