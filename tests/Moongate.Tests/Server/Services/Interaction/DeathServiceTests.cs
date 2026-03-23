using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Types.Items;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class DeathServiceTests
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
        public Dictionary<Serial, UOMobileEntity> Mobiles { get; } = [];
        public List<Serial> DeletedIds { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            Mobiles[mobile.Id] = mobile;

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            DeletedIds.Add(id);

            return Task.FromResult(Mobiles.Remove(id));
        }

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
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

    private sealed class InMemoryItemService : IItemService
    {
        private uint _nextId = 0x40000100u;

        public Dictionary<Serial, UOItemEntity> Items { get; } = [];
        public List<Serial> DeletedIds { get; } = [];

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
            if (item.Id == Serial.Zero)
            {
                item.Id = (Serial)_nextId++;
            }

            Items[item.Id] = item;

            return Task.FromResult(item.Id);
        }

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedIds.Add(itemId);

            return Task.FromResult(Items.Remove(itemId));
        }

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => Task.FromResult(new List<UOItemEntity>());

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
        {
            Items.TryGetValue(itemId, out var item);

            return Task.FromResult(item);
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => Task.FromResult(
                Items.Values
                     .Where(item => item.ParentContainerId == containerId)
                     .ToList()
            );

        public Task<bool> MoveItemToContainerAsync(
            Serial itemId,
            Serial containerId,
            Point2D position,
            long sessionId = 0
        )
        {
            if (!Items.TryGetValue(itemId, out var item) || !Items.TryGetValue(containerId, out var container))
            {
                return Task.FromResult(false);
            }

            if (item.ParentContainerId != Serial.Zero && Items.TryGetValue(item.ParentContainerId, out var oldContainer))
            {
                oldContainer.RemoveItem(item.Id);
            }

            item.EquippedMobileId = Serial.Zero;
            item.EquippedLayer = null;
            container.AddItem(item, position);
            item.MapId = container.MapId;
            Items[item.Id] = item;
            Items[container.Id] = container;

            return Task.FromResult(true);
        }

        public Task<bool> MoveItemToWorldAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        {
            var found = Items.TryGetValue(itemId, out var item);

            return Task.FromResult((found, item));
        }

        public Task UpsertItemAsync(UOItemEntity item)
        {
            Items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
        {
            foreach (var item in items)
            {
                Items[item.Id] = item;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SpatialWorldServiceSpy : ISpatialWorldService
    {
        public JsonRegion? Region { get; set; }
        public List<UOItemEntity> AddedItems { get; } = [];
        public List<Serial> RemovedEntities { get; } = [];
        public List<IGameNetworkPacket> BroadcastPackets { get; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = mapId;
            AddedItems.Add(item);
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

        public JsonRegion? ResolveRegion(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return Region;
        }
    }

    private sealed class RecordingEventBus : IGameEventBusService
    {
        public List<object> Events { get; } = [];

        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default)
            where TEvent : IGameEvent
        {
            _ = cancellationToken;
            Events.Add(gameEvent!);

            return ValueTask.CompletedTask;
        }

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : IGameEvent { }
    }

    private sealed class FameKarmaServiceSpy : IFameKarmaService
    {
        public List<(UOMobileEntity Victim, UOMobileEntity Killer)> Awards { get; } = [];

        public Task AwardNpcKillAsync(
            UOMobileEntity victim,
            UOMobileEntity killer,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            Awards.Add((victim, killer));

            return Task.CompletedTask;
        }
    }

    private sealed class LootGenerationServiceSpy : ILootGenerationService
    {
        public List<(Serial ContainerId, IReadOnlyList<string> LootTableIds, LootGenerationMode Mode)> Calls { get; } = [];

        public Task<UOItemEntity> EnsureLootGeneratedAsync(
            UOItemEntity container,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(container);

        public Task<UOItemEntity> GenerateForContainerAsync(
            UOItemEntity container,
            IReadOnlyList<string> lootTableIds,
            LootGenerationMode mode,
            CancellationToken cancellationToken = default
        )
        {
            Calls.Add((container.Id, lootTableIds, mode));

            var generatedItem = new UOItemEntity
            {
                Id = (Serial)0x40000099u,
                ItemId = 0x0EED,
                Name = "generated_gold",
                Amount = 200,
                MapId = container.MapId
            };
            container.AddItem(generatedItem, new(40, 40));

            return Task.FromResult(container);
        }
    }

    [Test]
    public async Task ForceDeathAsync_WhenNpcIsAlive_ShouldMarkDeadAndCreateCorpse()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000030u,
            Name = "Zombie",
            Body = 0x0003,
            MapId = 1,
            Location = new(90, 190, 5),
            Hits = 30,
            MaxHits = 30,
            IsAlive = true
        };
        mobileService.Mobiles[victim.Id] = victim;

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.ForceDeathAsync(victim, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(victim.IsAlive, Is.False);
                Assert.That(victim.Hits, Is.EqualTo(0));
                Assert.That(itemService.Items.Values.Any(item => item.ItemId == 0x2006), Is.True);
            }
        );
    }

    [Test]
    public async Task ForceDeathAsync_WhenPlayerIsAlive_ShouldMarkDeadAndPersistWithoutCorpse()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            Name = "Tommy",
            IsPlayer = true,
            MapId = 1,
            Location = new(95, 195, 0),
            Hits = 42,
            MaxHits = 42,
            IsAlive = true
        };
        mobileService.Mobiles[victim.Id] = victim;

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.ForceDeathAsync(victim, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(victim.IsAlive, Is.False);
                Assert.That(victim.Hits, Is.EqualTo(0));
                Assert.That(mobileService.Mobiles[victim.Id].IsAlive, Is.False);
                Assert.That(itemService.Items.Values.Any(item => item.ItemId == 0x2006), Is.False);
            }
        );
    }

    [Test]
    public async Task HandleDeathAsync_WhenCorpseDecayTimerFires_ShouldDeleteCorpseAndBroadcastDeleteObject()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 5
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000050u,
            Name = "Zombie",
            Body = 0x0003,
            MapId = 1,
            Location = new(140, 240, 0),
            Hits = 0,
            MaxHits = 30,
            IsAlive = false
        };
        mobileService.Mobiles[victim.Id] = victim;

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.HandleDeathAsync(victim, null);
        Assert.That(handled, Is.True);

        var corpse = itemService.Items.Values.Single(item => item.ItemId == 0x2006);

        timerService.RegisteredTimers[0].Callback.Invoke();

        Assert.Multiple(
            () =>
            {
                Assert.That(itemService.DeletedIds, Contains.Item(corpse.Id));
                Assert.That(spatial.RemovedEntities, Contains.Item(corpse.Id));
                Assert.That(
                    spatial.BroadcastPackets.OfType<DeleteObjectPacket>().Any(packet => packet.Serial == corpse.Id),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task HandleDeathAsync_WhenNpcDies_ShouldCreateCorpseMoveLootScheduleDecayAndBroadcastAnimation()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy
        {
            Region = new JsonTownRegion
            {
                Type = "TownRegion",
                Name = "Britain"
            }
        };
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000040u,
            Name = "Zombie",
            Body = 0x0003,
            MapId = 1,
            Location = new(100, 200, 5),
            Direction = DirectionType.East,
            SkinHue = 0x0835,
            Hits = 0,
            MaxHits = 30,
            IsAlive = false
        };
        var killer = new UOMobileEntity
        {
            Id = (Serial)0x00000041u,
            Name = "Tommy",
            IsPlayer = true,
            MapId = 1,
            Location = new(101, 200, 5)
        };
        var chest = new UOItemEntity
        {
            Id = (Serial)0x40000011u,
            ItemId = 0x1415,
            MapId = 1
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000012u,
            ItemId = 0x0E75,
            MapId = 1
        };
        var gold = new UOItemEntity
        {
            Id = (Serial)0x40000013u,
            ItemId = 0x0EED,
            Amount = 50,
            MapId = 1,
            ParentContainerId = backpack.Id
        };
        backpack.AddItem(gold, new(20, 30));
        victim.AddEquippedItem(ItemLayerType.InnerTorso, chest);
        victim.AddEquippedItem(ItemLayerType.Backpack, backpack);
        victim.BackpackId = backpack.Id;
        mobileService.Mobiles[victim.Id] = victim;
        itemService.Items[chest.Id] = chest;
        itemService.Items[backpack.Id] = backpack;
        itemService.Items[gold.Id] = gold;

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.HandleDeathAsync(victim, killer);

        var corpse = itemService.Items.Values.Single(item => item.ItemId == 0x2006);
        var corpseItems = corpse.Items.ToDictionary(item => item.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(corpse.MapId, Is.EqualTo(victim.MapId));
                Assert.That(corpse.Location, Is.EqualTo(victim.Location));
                Assert.That(corpse.Amount, Is.EqualTo((int)victim.Body));
                Assert.That(corpseItems.Keys, Does.Contain(chest.Id));
                Assert.That(corpseItems.Keys, Does.Contain(gold.Id));
                Assert.That(corpseItems[chest.Id].TryGetCustomInteger("corpse_equipped_layer", out var rawLayer), Is.True);
                Assert.That((ItemLayerType)rawLayer, Is.EqualTo(ItemLayerType.InnerTorso));
                Assert.That(mobileService.DeletedIds, Contains.Item(victim.Id));
                Assert.That(spatial.RemovedEntities, Contains.Item(victim.Id));
                Assert.That(spatial.AddedItems.Select(item => item.Id), Contains.Item(corpse.Id));
                Assert.That(timerService.RegisteredTimers, Has.Count.EqualTo(1));
                Assert.That(timerService.RegisteredTimers[0].Name, Is.EqualTo($"corpse-decay:{(uint)corpse.Id}"));
                Assert.That(timerService.RegisteredTimers[0].Interval, Is.EqualTo(TimeSpan.FromSeconds(300)));
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is MobileBeforeDeathEvent), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is MobileDeathEvent), Is.True);
                Assert.That(eventBus.Events.Any(gameEvent => gameEvent is MobileAfterDeathEvent), Is.True);
                Assert.That(
                    spatial.BroadcastPackets
                           .OfType<MobileDeathAnimationPacket>()
                           .Any(packet => packet.KilledMobileId == victim.Id && packet.CorpseId == corpse.Id),
                    Is.True
                );
                Assert.That(
                    spatial.BroadcastPackets.OfType<ObjectInformationPacket>().Any(packet => packet.Serial == corpse.Id),
                    Is.True
                );
                Assert.That(
                    spatial.BroadcastPackets
                           .OfType<AddMultipleItemsToContainerPacket>()
                           .Any(packet => packet.Container.Id == corpse.Id),
                    Is.True
                );
                Assert.That(
                    spatial.BroadcastPackets.OfType<CorpseClothingPacket>().Any(packet => packet.Corpse?.Id == corpse.Id),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task HandleDeathAsync_WhenNpcDiesWithPlayerKiller_ShouldAwardFameAndKarma()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000060u,
            Name = "Ogre",
            MapId = 1,
            Location = new(120, 220, 0),
            Hits = 0,
            MaxHits = 50,
            IsAlive = false,
            Fame = 3000,
            Karma = -3000
        };
        var killer = new UOMobileEntity
        {
            Id = (Serial)0x00000061u,
            Name = "Tommy",
            IsPlayer = true,
            MapId = 1,
            Location = new(121, 220, 0)
        };

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.HandleDeathAsync(victim, killer);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(fameKarmaService.Awards, Has.Count.EqualTo(1));
                Assert.That(fameKarmaService.Awards[0].Victim, Is.SameAs(victim));
                Assert.That(fameKarmaService.Awards[0].Killer, Is.SameAs(killer));
            }
        );
    }

    [Test]
    public async Task HandleDeathAsync_WhenNpcHasLootTables_ShouldGenerateAdditiveCorpseLootAfterTransfer()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var lootGenerationService = new LootGenerationServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000045u,
            Name = "Skeleton",
            Body = 0x0032,
            MapId = 1,
            Location = new(150, 250, 0),
            Hits = 0,
            MaxHits = 40,
            IsAlive = false
        };
        victim.SetCustomString("mobile_loot_tables", "undead.low,gold.small");

        var weapon = new UOItemEntity
        {
            Id = (Serial)0x40000021u,
            ItemId = 0x13B2,
            MapId = 1
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x40000022u,
            ItemId = 0x0E75,
            MapId = 1
        };
        var bandage = new UOItemEntity
        {
            Id = (Serial)0x40000023u,
            ItemId = 0x0E21,
            Amount = 10,
            MapId = 1,
            ParentContainerId = backpack.Id
        };
        backpack.AddItem(bandage, new(10, 10));
        victim.AddEquippedItem(ItemLayerType.OneHanded, weapon);
        victim.AddEquippedItem(ItemLayerType.Backpack, backpack);
        victim.BackpackId = backpack.Id;
        mobileService.Mobiles[victim.Id] = victim;
        itemService.Items[weapon.Id] = weapon;
        itemService.Items[backpack.Id] = backpack;
        itemService.Items[bandage.Id] = bandage;

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config,
            lootGenerationService: lootGenerationService
        );

        var handled = await service.HandleDeathAsync(victim, null);

        var corpse = itemService.Items.Values.Single(item => item.ItemId == 0x2006);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(corpse.Items.Select(static item => item.Id), Contains.Item(weapon.Id));
                Assert.That(corpse.Items.Select(static item => item.Id), Contains.Item(bandage.Id));
                Assert.That(corpse.Items.Any(static item => item.Name == "generated_gold"), Is.True);
                Assert.That(lootGenerationService.Calls, Has.Count.EqualTo(1));
                Assert.That(lootGenerationService.Calls[0].ContainerId, Is.EqualTo(corpse.Id));
                Assert.That(lootGenerationService.Calls[0].LootTableIds, Is.EqualTo(new[] { "undead.low", "gold.small" }));
                Assert.That(lootGenerationService.Calls[0].Mode, Is.EqualTo(LootGenerationMode.OnDeath));
            }
        );
    }

    [Test]
    public async Task HandleDeathAsync_WhenPlayerDies_ShouldNotAwardFameAndKarma()
    {
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var timerService = new TimerServiceSpy();
        var spatial = new SpatialWorldServiceSpy();
        var eventBus = new RecordingEventBus();
        var fameKarmaService = new FameKarmaServiceSpy();
        var config = new MoongateConfig
        {
            Game = new()
            {
                CorpseDecaySeconds = 300
            }
        };
        var victim = new UOMobileEntity
        {
            Id = (Serial)0x00000062u,
            Name = "Victim",
            IsPlayer = true,
            MapId = 1,
            Location = new(130, 230, 0),
            Hits = 0,
            MaxHits = 50,
            IsAlive = false,
            Fame = 3000,
            Karma = -3000
        };
        var killer = new UOMobileEntity
        {
            Id = (Serial)0x00000063u,
            Name = "Tommy",
            IsPlayer = true,
            MapId = 1,
            Location = new(131, 230, 0)
        };

        IDeathService service = new DeathService(
            mobileService,
            itemService,
            spatial,
            timerService,
            eventBus,
            fameKarmaService,
            config
        );

        var handled = await service.HandleDeathAsync(victim, killer);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(fameKarmaService.Awards, Is.Empty);
            }
        );
    }
}
