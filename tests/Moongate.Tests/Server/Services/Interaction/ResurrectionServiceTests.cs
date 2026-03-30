using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Races;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class ResurrectionServiceTests
{
    [Test]
    public async Task TryResurrectAsync_WhenDeadPlayerFitsLocation_ShouldRestoreAliveStateDeleteShroudAndPublishAppearanceChanged()
    {
        EnsureRaceDefinitionsRegistered();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            Name = "Tommy",
            IsPlayer = true,
            IsAlive = false,
            MapId = 1,
            Location = new(125, 225, 0),
            Gender = GenderType.Male,
            Race = Race.Human,
            BaseBody = 0x00,
            Hits = 0,
            MaxHits = 42
        };
        var shroud = new UOItemEntity
        {
            Id = (Serial)0x40000031u,
            ItemId = 0x204E,
            Name = "Death Shroud",
            MapId = character.MapId,
            Location = character.Location
        };
        var corpse = new UOItemEntity
        {
            Id = (Serial)0x40000032u,
            ItemId = 0x2006,
            Name = "Tommy's corpse",
            MapId = character.MapId,
            Location = character.Location
        };
        var healer = new UOMobileEntity
        {
            Id = (Serial)0x00009001u,
            IsPlayer = false,
            IsAlive = true,
            MapId = character.MapId,
            Location = new Point3D(127, 226, 0)
        };
        healer.SetCustomString(
            Moongate.Server.Data.Internal.Scripting.MobileCustomParamKeys.Interaction.ResurrectionSource,
            "healer"
        );
        character.AddEquippedItem(ItemLayerType.OuterTorso, shroud);
        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var mobileService = new TestMobileService();
        mobileService.Mobiles[healer.Id] = healer;
        var itemService = new TestItemService();
        itemService.Items[shroud.Id] = shroud;
        itemService.Items[corpse.Id] = corpse;
        var movementTileQueryService = new TestMovementTileQueryService();
        var eventBus = new TestGameEventBusService();
        var service = new ResurrectionService(
            sessionService,
            mobileService,
            itemService,
            movementTileQueryService,
            eventBus
        );

        var resurrected = await service.TryResurrectAsync(
            session.SessionId,
            character.Id,
            ResurrectionOfferSourceType.Healer,
            (Serial)0x00009001u,
            character.MapId,
            new Point3D(127, 226, 0)
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(resurrected, Is.True);
                Assert.That(character.IsAlive, Is.True);
                Assert.That(character.Hits, Is.EqualTo(4));
                Assert.That(character.BaseBody, Is.EqualTo((Body)0x00));
                Assert.That(character.Body, Is.EqualTo((Body)Race.Human.AliveBody(false)));
                Assert.That(character.EquippedItemIds.ContainsKey(ItemLayerType.OuterTorso), Is.False);
                Assert.That(itemService.DeletedIds, Contains.Item(shroud.Id));
                Assert.That(itemService.DeletedIds, Does.Not.Contain(corpse.Id));
                Assert.That(itemService.Items.ContainsKey(corpse.Id), Is.True);
                Assert.That(mobileService.UpsertedMobiles, Has.Count.EqualTo(1));
                Assert.That(mobileService.UpsertedMobiles[0], Is.SameAs(character));
                Assert.That(
                    eventBus.Events.OfType<MobileAppearanceChangedEvent>().Any(gameEvent => gameEvent.Mobile == character),
                    Is.True
                );
            }
        );
    }

    [Test]
    public async Task TryResurrectAsync_WhenLocationCannotFit_ShouldReturnFalse()
    {
        EnsureRaceDefinitionsRegistered();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000041u,
            Name = "Blocked",
            IsPlayer = true,
            IsAlive = false,
            MapId = 1,
            Location = new(200, 300, 0),
            Gender = GenderType.Male,
            Race = Race.Human,
            BaseBody = 0x00,
            Hits = 0,
            MaxHits = 30
        };
        var shroud = new UOItemEntity
        {
            Id = (Serial)0x40000041u,
            ItemId = 0x204E,
            Name = "Death Shroud",
            MapId = character.MapId,
            Location = character.Location
        };
        var ankh = new UOItemEntity
        {
            Id = (Serial)0x00009002u,
            ItemId = 0x0004,
            Name = "Ankh",
            MapId = character.MapId,
            Location = character.Location
        };
        ankh.SetCustomString(
            Moongate.Server.Data.Internal.Scripting.ItemCustomParamKeys.Interaction.ResurrectionSource,
            "ankh"
        );
        character.AddEquippedItem(ItemLayerType.OuterTorso, shroud);
        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var mobileService = new TestMobileService();
        var itemService = new TestItemService();
        itemService.Items[shroud.Id] = shroud;
        itemService.Items[ankh.Id] = ankh;
        var movementTileQueryService = new TestMovementTileQueryService
        {
            CanFitResult = false
        };
        var eventBus = new TestGameEventBusService();
        var service = new ResurrectionService(
            sessionService,
            mobileService,
            itemService,
            movementTileQueryService,
            eventBus
        );

        var resurrected = await service.TryResurrectAsync(
            session.SessionId,
            character.Id,
            ResurrectionOfferSourceType.Ankh,
            (Serial)0x00009002u,
            character.MapId,
            character.Location
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(resurrected, Is.False);
                Assert.That(character.IsAlive, Is.False);
                Assert.That(character.EquippedItemIds.ContainsKey(ItemLayerType.OuterTorso), Is.True);
                Assert.That(itemService.DeletedIds, Is.Empty);
                Assert.That(mobileService.UpsertedMobiles, Is.Empty);
                Assert.That(eventBus.Events, Is.Empty);
            }
        );
    }

    [Test]
    public async Task TryResurrectAsync_WhenPendingOfferCharacterDoesNotMatchSession_ShouldReturnFalse()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000051u,
            IsPlayer = true,
            IsAlive = false,
            MapId = 1,
            Location = new(10, 10, 0)
        };
        var session = new GameSession(new(client))
        {
            Character = character,
            CharacterId = character.Id
        };
        var sessionService = new TestGameNetworkSessionService();
        sessionService.Add(session);
        var service = new ResurrectionService(
            sessionService,
            new TestMobileService(),
            new TestItemService(),
            new TestMovementTileQueryService(),
            new TestGameEventBusService()
        );

        var resurrected = await service.TryResurrectAsync(
            session.SessionId,
            (Serial)0x00000052u,
            ResurrectionOfferSourceType.Healer,
            (Serial)0x00009003u,
            character.MapId,
            character.Location
        );

        Assert.That(resurrected, Is.False);
    }

    private sealed class TestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly List<GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions.Add(session);

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => _sessions.ToArray();

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
        {
            var removed = _sessions.RemoveAll(session => session.SessionId == sessionId);

            return removed > 0;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.SessionId == sessionId)!;

            return session is not null;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            session = _sessions.FirstOrDefault(candidate => candidate.CharacterId == characterId)!;

            return session is not null;
        }
    }

    private sealed class TestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> Mobiles { get; } = [];

        public List<UOMobileEntity> UpsertedMobiles { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            UpsertedMobiles.Add(mobile);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(Mobiles.GetValueOrDefault(id));

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

    private sealed class TestItemService : IItemService
    {
        public Dictionary<Serial, UOItemEntity> Items { get; } = [];
        public List<Serial> DeletedIds { get; } = [];

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeletedIds.Add(itemId);

            return Task.FromResult(Items.Remove(itemId));
        }

        public Task<Moongate.Server.Data.Items.DropItemToGroundResult?> DropItemToGroundAsync(
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
            => Task.FromResult(new List<UOItemEntity>());

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
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
            => Task.CompletedTask;
    }

    private sealed class TestMovementTileQueryService : IMovementTileQueryService
    {
        public bool CanFitResult { get; set; } = true;

        public bool CanFit(
            int mapId,
            int x,
            int y,
            int z,
            int height = 16,
            bool checkBlocksFit = false,
            bool checkMobiles = true,
            bool requireSurface = true
        )
            => CanFitResult;

        public IReadOnlyList<Moongate.UO.Data.Tiles.StaticTile> GetStaticTiles(int mapId, int x, int y)
            => [];

        public bool TryGetLandTile(int mapId, int x, int y, out Moongate.UO.Data.Tiles.LandTile landTile)
        {
            landTile = default;

            return false;
        }

        public bool TryGetMapBounds(int mapId, out int width, out int height)
        {
            width = 0;
            height = 0;

            return false;
        }
    }

    private sealed class TestGameEventBusService : IGameEventBusService
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

    private static void EnsureRaceDefinitionsRegistered()
    {
        if (Race.Races[0] is null)
        {
            RaceDefinitions.RegisterRace(new Human(0, 0));
        }

        if (Race.Races[1] is null)
        {
            RaceDefinitions.RegisterRace(new Elf(1, 1));
        }

        if (Race.Races[2] is null)
        {
            RaceDefinitions.RegisterRace(new Gargoyle(2, 2));
        }
    }
}
