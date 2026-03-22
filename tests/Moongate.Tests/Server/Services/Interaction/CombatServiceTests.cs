using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Interaction;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
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
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

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
        public Func<Serial, UOMobileEntity?, UOMobileEntity?>? OnGetAsync { get; set; }

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
            return Task.FromResult(OnGetAsync?.Invoke(id, mobile) ?? mobile);
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

    private sealed class InMemoryItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = new();

        public void Add(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
        {
            foreach (var item in items)
            {
                _items[item.Id] = item;
            }

            return Task.CompletedTask;
        }

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromException<UOItemEntity?>(new NotSupportedException());

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromException<Serial>(new NotSupportedException());

        public Task<bool> DeleteItemAsync(Serial itemId)
            => Task.FromResult(_items.Remove(itemId));

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => Task.FromException<DropItemToGroundResult?>(new NotSupportedException());

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => Task.FromException<bool>(new NotSupportedException());

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        {
            _items.TryGetValue(itemId, out var item);

            return Task.FromResult(item);
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(_items.Values.Where(item => item.ParentContainerId == containerId).ToList());

        public Task<bool> MoveItemToContainerAsync(
            Serial itemId,
            Serial containerId,
            Point2D position,
            long sessionId = 0
        )
            => Task.FromException<bool>(new NotSupportedException());

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => Task.FromException<bool>(new NotSupportedException());

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => Task.FromException<UOItemEntity>(new NotSupportedException());

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => Task.FromResult((_items.TryGetValue(itemId, out var item), item));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => BulkUpsertItemsAsync(items);
    }

    private sealed class DeathServiceSpy : IDeathService
    {
        public List<(UOMobileEntity Victim, UOMobileEntity? Killer)> Calls { get; } = [];

        public Task<bool> ForceDeathAsync(
            UOMobileEntity victim,
            UOMobileEntity? killer,
            CancellationToken cancellationToken = default
        )
            => HandleDeathAsync(victim, killer, cancellationToken);

        public Task<bool> HandleDeathAsync(
            UOMobileEntity victim,
            UOMobileEntity? killer,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            Calls.Add((victim, killer));

            return Task.FromResult(true);
        }
    }

    private sealed class SkillGainServiceSpy : ISkillGainService
    {
        public List<(Serial MobileId, UOSkillName SkillName, double SuccessChance, bool WasSuccessful)> Calls { get; } = [];

        public bool MutateSkillOnCall { get; set; }

        public SkillGainResult TryGain(
            UOMobileEntity mobile,
            UOSkillName skillName,
            double successChance,
            bool wasSuccessful
        )
        {
            Calls.Add((mobile.Id, skillName, successChance, wasSuccessful));

            if (MutateSkillOnCall)
            {
                var entry = mobile.GetSkill(skillName);

                if (entry is not null)
                {
                    entry.Base += 1;
                    entry.Value += 1;
                }
            }

            return new SkillGainResult(skillName, MutateSkillOnCall, null);
        }
    }

    private sealed class CombatTestSpatialWorldService : ISpatialWorldService
    {
        private readonly Dictionary<(int MapId, int SectorX, int SectorY), MapSector> _sectors = [];

        public JsonRegion? ResolvedRegion { get; set; }
        public List<IGameNetworkPacket> BroadcastPackets { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
        {
            RemoveEntity(mobile.Id);
            var sectorX = mobile.Location.X >> MapSectorConsts.SectorShift;
            var sectorY = mobile.Location.Y >> MapSectorConsts.SectorShift;
            var key = (mobile.MapId, sectorX, sectorY);

            if (!_sectors.TryGetValue(key, out var sector))
            {
                sector = new MapSector(mobile.MapId, sectorX, sectorY);
                _sectors[key] = sector;
            }

            sector.AddEntity(mobile);
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
            _ = excludeSessionId;
            BroadcastPackets.Add(packet);
            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => _sectors.Values.ToList();

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            var mobiles = new List<UOMobileEntity>();

            foreach (var sector in _sectors.Values)
            {
                if (sector.MapIndex != mapId)
                {
                    continue;
                }

                if (Math.Abs(sector.SectorX - centerSectorX) > radius || Math.Abs(sector.SectorY - centerSectorY) > radius)
                {
                    continue;
                }

                mobiles.AddRange(sector.GetMobiles());
            }

            return mobiles;
        }

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => _sectors.Values
                       .Where(sector => sector.MapIndex == mapId)
                       .SelectMany(sector => sector.GetEntitiesInRange<UOMobileEntity>(location, range))
                       .ToList();

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
        public void RemoveEntity(Serial serial)
        {
            foreach (var sector in _sectors.Values)
            {
                var mobile = sector.GetEntity<UOMobileEntity>(serial);

                if (mobile is not null)
                {
                    sector.RemoveEntity(mobile);
                }
            }
        }

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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(spatial.BroadcastPackets.Any(packet => packet is FightOccurringPacket), Is.True);
                Assert.That(defender.Hits, Is.EqualTo(34));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is AggressiveActionEvent), Is.True);
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
        var aggressiveActionEvent = eventBus.Events.OfType<AggressiveActionEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(hitEvent.Attacker, Is.SameAs(attacker));
                Assert.That(hitEvent.Defender, Is.SameAs(defender));
                Assert.That(aggressiveActionEvent.Attacker, Is.SameAs(attacker));
                Assert.That(aggressiveActionEvent.Defender, Is.SameAs(defender));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenPlayerAttackResolves_ShouldAttemptCombatSkillGainAndRefreshSkillList()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var skillGainService = new SkillGainServiceSpy
        {
            MutateSkillOnCall = true
        };

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            MinWeaponDamage = 6,
            MaxWeaponDamage = 6
        };
        attacker.InitializeSkills();
        attacker.SetSkill(UOSkillName.Wrestling, 500);
        attacker.SetSkill(UOSkillName.Tactics, 500);
        attacker.SetSkill(UOSkillName.Anatomy, 500);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000032u,
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy(),
            skillGainService
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();
        var outboundPackets = new List<IGameNetworkPacket>();

        while (outgoingQueue.TryDequeue(out var outbound))
        {
            outboundPackets.Add(outbound.Packet);
        }

        Assert.Multiple(
            () =>
            {
                Assert.That(skillGainService.Calls, Has.Count.EqualTo(3));
                Assert.That(skillGainService.Calls.Select(static call => call.SkillName), Is.EquivalentTo(
                    new[] { UOSkillName.Wrestling, UOSkillName.Tactics, UOSkillName.Anatomy }
                ));
                Assert.That(session.Character!.GetSkill(UOSkillName.Wrestling)!.Base, Is.EqualTo(501));
                Assert.That(outboundPackets.Any(static packet => packet is SkillListPacket), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenRangedWeaponHasAmmoAndTargetIsWithinWeaponRange_ShouldConsumeAmmoAndBroadcastProjectile()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000030u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 1000);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000030u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 10
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000031u,
            ItemId = 0x0E75,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Backpack
        };
        var arrows = new UOItemEntity
        {
            Id = (Serial)0x40000032u,
            ItemId = 0x0F3F,
            Amount = 3,
            IsStackable = true
        };
        backpack.AddItem(arrows, Point2D.Zero);
        attacker.BackpackId = backpack.Id;
        attacker.HydrateEquipmentRuntime([bow, backpack]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(backpack);
        itemService.Add(arrows);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);
        _ = outgoingQueue.TryDequeue(out _);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        var projectile = spatial.BroadcastPackets.OfType<GraphicalEffectPacket>().Single();
        var queuedPackets = DequeuePackets(outgoingQueue);

        Assert.Multiple(
            () =>
            {
                Assert.That(defender.Hits, Is.EqualTo(34));
                Assert.That(arrows.Amount, Is.EqualTo(2));
                Assert.That(queuedPackets.OfType<AddMultipleItemsToContainerPacket>().Any(), Is.True);
                Assert.That(projectile.ItemId, Is.EqualTo(0x1BFE));
                Assert.That(projectile.SourceId, Is.EqualTo(attacker.Id));
                Assert.That(projectile.TargetId, Is.EqualTo(defender.Id));
                Assert.That(projectile.SourceLocation, Is.EqualTo(attacker.Location));
                Assert.That(projectile.TargetLocation, Is.EqualTo(defender.Location));
                Assert.That(spatial.BroadcastPackets.Any(packet => packet is FightOccurringPacket), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenNpcArcherWeaponExistsOnlyInSpatialRuntime_ShouldStillUseRangedCombatProfile()
    {
        EnsureMapsRegistered();
        Body.Types = new UOBodyType[0x200];
        Body.Types[0x0190] = UOBodyType.Human;
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var liveAttacker = new UOMobileEntity
        {
            Id = (Serial)0x00000050u,
            IsPlayer = false,
            MapId = 0,
            Location = new(100, 100, 0),
            Direction = DirectionType.West,
            BaseBody = 0x0190,
            Hits = 50,
            MaxHits = 50
        };
        liveAttacker.SetSkill(UOSkillName.Archery, 1000);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000050u,
            ItemId = 0x13B2,
            EquippedMobileId = liveAttacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 10
            }
        };
        liveAttacker.HydrateEquipmentRuntime([bow]);

        var liveDefender = new UOMobileEntity
        {
            Id = (Serial)0x00000051u,
            IsPlayer = false,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        spatial.AddOrUpdateMobile(liveAttacker);
        spatial.AddOrUpdateMobile(liveDefender);
        mobileService.Add(CreatePersistenceClone(liveAttacker));
        mobileService.Add(CreatePersistenceClone(liveDefender));

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(liveAttacker.Id, liveDefender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(liveDefender.Hits, Is.EqualTo(34));
                Assert.That(liveAttacker.Direction, Is.EqualTo(DirectionType.East));
                Assert.That(spatial.BroadcastPackets.OfType<GraphicalEffectPacket>().Any(), Is.True);
                Assert.That(spatial.BroadcastPackets.OfType<FightOccurringPacket>().Any(), Is.True);
                Assert.That(spatial.BroadcastPackets.OfType<MobileMovingPacket>().Any(packet => packet.Mobile?.Id == liveAttacker.Id), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is MobilePlayAnimationEvent), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenRangedWeaponHasNoAmmo_ShouldClearCombatantWithoutApplyingDamage()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000032u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000033u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 10
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000034u,
            ItemId = 0x0E75,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Backpack
        };
        attacker.BackpackId = backpack.Id;
        attacker.HydrateEquipmentRuntime([bow, backpack]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000033u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(backpack);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(attacker.CombatantId, Is.EqualTo(Serial.Zero));
                Assert.That(defender.Hits, Is.EqualTo(40));
                Assert.That(spatial.BroadcastPackets.OfType<GraphicalEffectPacket>(), Is.Empty);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent or CombatMissEvent), Is.False);
            }
        );
    }

    [Test]
    public async Task TrySetCombatantAsync_WhenAlreadyTargetingSameDefender_ShouldNotRescheduleSwing()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000038u,
            IsPlayer = false,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        var sword = new UOItemEntity
        {
            Id = (Serial)0x40000038u,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.OneHanded,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 1,
                AttackSpeed = 30
            }
        };
        attacker.HydrateEquipmentRuntime([sword]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000039u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var firstSetTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        var firstNextCombatAtUtc = attacker.NextCombatAtUtc;
        var firstRegisteredTimerCount = timerService.RegisteredTimers.Count;

        var secondSetTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(firstSetTarget, Is.True);
                Assert.That(secondSetTarget, Is.True);
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(firstRegisteredTimerCount));
                Assert.That(attacker.NextCombatAtUtc, Is.EqualTo(firstNextCombatAtUtc));
            }
        );
    }

    [Test]
    public async Task TrySetCombatantAsync_WhenWarmodeAndCombatantArePresetWithoutScheduledSwing_ShouldStillScheduleFirstAttack()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x0000003Au,
            IsPlayer = false,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            CombatantId = (Serial)0x0000003Bu,
            Warmode = true
        };
        var sword = new UOItemEntity
        {
            Id = (Serial)0x4000003Bu,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.OneHanded,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 1,
                AttackSpeed = 30
            }
        };
        attacker.HydrateEquipmentRuntime([sword]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x0000003Bu,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        spatial.AddOrUpdateMobile(attacker);
        spatial.AddOrUpdateMobile(defender);

        ICombatService service = new CombatService(
            mobileService,
            sessionService,
            outgoingQueue,
            timerService,
            spatial,
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var result = await service.TrySetCombatantAsync(attacker.Id, defender.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.RegisteredTimers[0].Name, Is.EqualTo($"combat:{(uint)attacker.Id}"));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenRangedArcheryIsOutmatched_ShouldPublishMissAndStillConsumeAmmo()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000034u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 0);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000035u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new()
            {
                DamageMin = 6,
                DamageMax = 6,
                RangeMin = 1,
                RangeMax = 10
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000036u,
            ItemId = 0x0E75,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Backpack
        };
        var arrows = new UOItemEntity
        {
            Id = (Serial)0x40000037u,
            ItemId = 0x0F3F,
            Amount = 2,
            IsStackable = true
        };
        backpack.AddItem(arrows, Point2D.Zero);
        attacker.BackpackId = backpack.Id;
        attacker.HydrateEquipmentRuntime([bow, backpack]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000035u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };
        defender.SetSkill(UOSkillName.Wrestling, 1000);

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(backpack);
        itemService.Add(arrows);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(defender.Hits, Is.EqualTo(40));
                Assert.That(arrows.Amount, Is.EqualTo(1));
                Assert.That(spatial.BroadcastPackets.OfType<GraphicalEffectPacket>().Count(), Is.EqualTo(1));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatMissEvent), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent), Is.False);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenRangedWeaponConsumesLastAmmo_ShouldEnqueueDeleteObjectForConsumedStack()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000036u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 1000);
        var crossbow = new UOItemEntity
        {
            Id = (Serial)0x40000038u,
            ItemId = 0x0F50,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x1BFB,
            AmmoEffectId = 0x1BFB,
            CombatStats = new()
            {
                DamageMin = 8,
                DamageMax = 8,
                RangeMin = 1,
                RangeMax = 8
            }
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000039u,
            ItemId = 0x0E75,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Backpack
        };
        var bolt = new UOItemEntity
        {
            Id = (Serial)0x4000003Au,
            ItemId = 0x1BFB,
            Amount = 1,
            IsStackable = true
        };
        backpack.AddItem(bolt, Point2D.Zero);
        attacker.BackpackId = backpack.Id;
        attacker.HydrateEquipmentRuntime([crossbow, backpack]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000037u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(backpack);
        itemService.Add(bolt);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);
        _ = outgoingQueue.TryDequeue(out _);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        var queuedPackets = DequeuePackets(outgoingQueue);

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.GetItemAsync(bolt.Id).Result, Is.Null);
                Assert.That(queuedPackets.OfType<DeleteObjectPacket>().Any(packet => packet.Serial == bolt.Id), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenEquippedQuiverHasMatchingAmmo_ShouldConsumeQuiverBeforeBackpack()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000040u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 1000);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000040u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new() { DamageMin = 6, DamageMax = 6, RangeMin = 1, RangeMax = 10 }
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x40000041u,
            ItemId = 0x2B02,
            IsQuiver = true,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Cloak,
            GumpId = 0x0108
        };
        var quiverArrows = new UOItemEntity
        {
            Id = (Serial)0x40000042u,
            ItemId = 0x0F3F,
            Amount = 2,
            IsStackable = true
        };
        quiver.AddItem(quiverArrows, Point2D.Zero);
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000043u,
            ItemId = 0x0E75,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Backpack
        };
        var backpackArrows = new UOItemEntity
        {
            Id = (Serial)0x40000044u,
            ItemId = 0x0F3F,
            Amount = 5,
            IsStackable = true
        };
        backpack.AddItem(backpackArrows, Point2D.Zero);
        attacker.BackpackId = backpack.Id;
        attacker.HydrateEquipmentRuntime([bow, quiver, backpack]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000041u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(quiver);
        itemService.Add(quiverArrows);
        itemService.Add(backpack);
        itemService.Add(backpackArrows);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);
        _ = outgoingQueue.TryDequeue(out _);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        var queuedPackets = DequeuePackets(outgoingQueue);

        Assert.Multiple(
            () =>
            {
                Assert.That(quiverArrows.Amount, Is.EqualTo(1));
                Assert.That(backpackArrows.Amount, Is.EqualTo(5));
                Assert.That(queuedPackets.OfType<AddMultipleItemsToContainerPacket>().Any(packet => packet.Container?.Id == quiver.Id), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenQuiverLowerAmmoCostIsGuaranteed_ShouldNotConsumeAmmo()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000042u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 1000);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000045u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new() { DamageMin = 6, DamageMax = 6, RangeMin = 1, RangeMax = 10 }
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x40000046u,
            ItemId = 0x2B02,
            IsQuiver = true,
            QuiverLowerAmmoCost = 100,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Cloak
        };
        var quiverArrows = new UOItemEntity
        {
            Id = (Serial)0x40000047u,
            ItemId = 0x0F3F,
            Amount = 2,
            IsStackable = true
        };
        quiver.AddItem(quiverArrows, Point2D.Zero);
        attacker.HydrateEquipmentRuntime([bow, quiver]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000043u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(quiver);
        itemService.Add(quiverArrows);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);
        _ = outgoingQueue.TryDequeue(out _);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(quiverArrows.Amount, Is.EqualTo(2));
                Assert.That(spatial.BroadcastPackets.OfType<GraphicalEffectPacket>().Count(), Is.EqualTo(1));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent or CombatMissEvent), Is.True);
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenQuiverDamageIncreaseIsPresent_ShouldIncreaseRangedDamage()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000044u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50
        };
        attacker.SetSkill(UOSkillName.Archery, 1000);
        var bow = new UOItemEntity
        {
            Id = (Serial)0x40000048u,
            ItemId = 0x13B2,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.TwoHanded,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            CombatStats = new() { DamageMin = 10, DamageMax = 10, RangeMin = 1, RangeMax = 10 }
        };
        var quiver = new UOItemEntity
        {
            Id = (Serial)0x40000049u,
            ItemId = 0x2B02,
            IsQuiver = true,
            QuiverDamageIncrease = 20,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.Cloak
        };
        var quiverArrows = new UOItemEntity
        {
            Id = (Serial)0x4000004Au,
            ItemId = 0x0F3F,
            Amount = 2,
            IsStackable = true
        };
        quiver.AddItem(quiverArrows, Point2D.Zero);
        attacker.HydrateEquipmentRuntime([bow, quiver]);

        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000045u,
            MapId = 0,
            Location = new(105, 100, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        itemService.Add(quiver);
        itemService.Add(quiverArrows);

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
            eventBus,
            itemService,
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);
        _ = outgoingQueue.TryDequeue(out _);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.That(defender.Hits, Is.EqualTo(28));
    }

    [Test]
    public async Task ScheduledSwing_WhenAttackerHasStrengthAnatomyAndTactics_ShouldIncreaseDamageUsingAosLikeScaling()
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
            Id = (Serial)0x00000020u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            Strength = 100
        };
        attacker.SetSkill(UOSkillName.Anatomy, 1000);
        attacker.SetSkill(UOSkillName.Tactics, 1000);
        var weapon = new UOItemEntity
        {
            Id = (Serial)0x40000020u,
            ItemId = 0x13B6,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.OneHanded,
            CombatStats = new()
            {
                DamageMin = 10,
                DamageMax = 10
            }
        };
        attacker.HydrateEquipmentRuntime([weapon]);
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000021u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 80,
            MaxHits = 80
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.That(defender.Hits, Is.EqualTo(55));
    }

    [Test]
    public async Task ScheduledSwing_WhenDamageIncreaseExceedsOneHundred_ShouldCapDisplayedDamageBonusAtOneHundredPercent()
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
            Id = (Serial)0x00000022u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            RuntimeModifiers = new()
            {
                DamageIncrease = 180
            }
        };
        var weapon = new UOItemEntity
        {
            Id = (Serial)0x40000022u,
            ItemId = 0x13B6,
            EquippedMobileId = attacker.Id,
            EquippedLayer = ItemLayerType.OneHanded,
            CombatStats = new()
            {
                DamageMin = 10,
                DamageMax = 10
            }
        };
        attacker.HydrateEquipmentRuntime([weapon]);
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000023u,
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.That(defender.Hits, Is.EqualTo(20));
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
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

    [Test]
    public async Task ScheduledSwing_WhenHitIsLethal_ShouldDelegateToDeathService()
    {
        EnsureMapsRegistered();
        var mobileService = new InMemoryMobileService();
        var timerService = new TimerServiceSpy();
        var spatial = new CombatTestSpatialWorldService();
        var eventBus = new RecordingGameEventBusService();
        var outgoingQueue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var deathService = new DeathServiceSpy();

        var attacker = new UOMobileEntity
        {
            Id = (Serial)0x00000012u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            MinWeaponDamage = 10,
            MaxWeaponDamage = 10
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000013u,
            MapId = 0,
            Location = new(101, 100, 0),
            Hits = 10,
            MaxHits = 10
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
            eventBus,
            new InMemoryItemService(),
            deathService
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(deathService.Calls, Has.Count.EqualTo(1));
                Assert.That(deathService.Calls[0].Victim, Is.SameAs(defender));
                Assert.That(deathService.Calls[0].Killer, Is.SameAs(attacker));
                Assert.That(attacker.CombatantId, Is.EqualTo(Serial.Zero));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenDefenderIsInnocentNpcOnHarmfulRestrictedMap_ShouldAllowAttack()
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
            Id = (Serial)0x00000040u,
            IsPlayer = true,
            MapId = 1,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 50,
            MinWeaponDamage = 6,
            MaxWeaponDamage = 6,
            Notoriety = Notoriety.Innocent
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000041u,
            IsPlayer = false,
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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(defender.Hits, Is.EqualTo(34));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is CombatHitEvent), Is.True);
                Assert.That(attacker.CombatantId, Is.EqualTo(defender.Id));
            }
        );
    }

    [Test]
    public async Task ScheduledSwing_WhenAttackerIsNotFacingDefender_ShouldTurnTowardTargetAndBroadcastFacingUpdate()
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
            Id = (Serial)0x00000052u,
            IsPlayer = true,
            MapId = 0,
            Location = new(100, 100, 0),
            Direction = DirectionType.South,
            Hits = 50,
            MaxHits = 50,
            MinWeaponDamage = 6,
            MaxWeaponDamage = 6
        };
        var defender = new UOMobileEntity
        {
            Id = (Serial)0x00000053u,
            IsPlayer = false,
            MapId = 0,
            Location = new(100, 99, 0),
            Hits = 40,
            MaxHits = 40
        };

        mobileService.Add(attacker);
        mobileService.Add(defender);
        spatial.AddOrUpdateMobile(attacker);
        spatial.AddOrUpdateMobile(defender);

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
            eventBus,
            new InMemoryItemService(),
            new DeathServiceSpy()
        );

        var setTarget = await service.TrySetCombatantAsync(attacker.Id, defender.Id);
        Assert.That(setTarget, Is.True);

        timerService.RegisteredTimers[^1].Callback.Invoke();

        var facingUpdate = spatial.BroadcastPackets.OfType<MobileMovingPacket>().Single(packet => packet.Mobile?.Id == attacker.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(attacker.Direction, Is.EqualTo(DirectionType.North));
                Assert.That(facingUpdate.Mobile!.Direction, Is.EqualTo(DirectionType.North));
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

    private static List<IGameNetworkPacket> DequeuePackets(BasePacketListenerTestOutgoingPacketQueue queue)
    {
        var packets = new List<IGameNetworkPacket>();

        while (queue.TryDequeue(out var outgoing))
        {
            packets.Add(outgoing.Packet);
        }

        return packets;
    }

    private static UOMobileEntity CreatePersistenceClone(UOMobileEntity mobile)
        => new()
        {
            Id = mobile.Id,
            AccountId = mobile.AccountId,
            Name = mobile.Name,
            Title = mobile.Title,
            BrainId = mobile.BrainId,
            Location = mobile.Location,
            MapId = mobile.MapId,
            Direction = mobile.Direction,
            IsPlayer = mobile.IsPlayer,
            IsAlive = mobile.IsAlive,
            Gender = mobile.Gender,
            RaceIndex = mobile.RaceIndex,
            ProfessionId = mobile.ProfessionId,
            SkinHue = mobile.SkinHue,
            HairStyle = mobile.HairStyle,
            HairHue = mobile.HairHue,
            FacialHairStyle = mobile.FacialHairStyle,
            FacialHairHue = mobile.FacialHairHue,
            BaseBody = mobile.BaseBody,
            BaseStats = mobile.BaseStats,
            BaseResistances = mobile.BaseResistances,
            Resources = mobile.Resources,
            EquipmentModifiers = mobile.EquipmentModifiers,
            RuntimeModifiers = mobile.RuntimeModifiers,
            ModifierCaps = mobile.ModifierCaps,
            BackpackId = mobile.BackpackId,
            EquippedItemIds = new(mobile.EquippedItemIds),
            IsWarMode = mobile.IsWarMode,
            Hunger = mobile.Hunger,
            Thirst = mobile.Thirst,
            Fame = mobile.Fame,
            Karma = mobile.Karma,
            Kills = mobile.Kills,
            IsHidden = mobile.IsHidden,
            IsFrozen = mobile.IsFrozen,
            IsParalyzed = mobile.IsParalyzed,
            IsFlying = mobile.IsFlying,
            IgnoreMobiles = mobile.IgnoreMobiles,
            IsPoisoned = mobile.IsPoisoned,
            IsBlessed = mobile.IsBlessed,
            IsInvulnerable = mobile.IsInvulnerable,
            Notoriety = mobile.Notoriety,
            CombatantId = mobile.CombatantId,
            NextCombatAtUtc = mobile.NextCombatAtUtc,
            LastCombatAtUtc = mobile.LastCombatAtUtc,
            MinWeaponDamage = mobile.MinWeaponDamage,
            MaxWeaponDamage = mobile.MaxWeaponDamage
        };
}
