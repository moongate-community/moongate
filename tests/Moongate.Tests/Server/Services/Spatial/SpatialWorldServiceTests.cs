using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Spatial;

public sealed class SpatialWorldServiceTests
{
    private readonly List<MoongateTCPClient> _clientsToDispose = [];

    private sealed class SpatialWorldServiceTestItemService : IItemService
    {
        public Dictionary<(int MapId, int SectorX, int SectorY), List<UOItemEntity>> ItemsBySector { get; } = [];
        public Dictionary<Serial, UOItemEntity> ItemsById { get; } = [];

        public List<(int MapId, int SectorX, int SectorY)> LoadRequests { get; } = [];

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        {
            LoadRequests.Add((mapId, sectorX, sectorY));
            ItemsBySector.TryGetValue((mapId, sectorX, sectorY), out var items);

            return Task.FromResult(items ?? []);
        }

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => Task.FromResult(ItemsById.TryGetValue(itemId, out var item) ? item : null);

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
        {
            var found = ItemsById.TryGetValue(itemId, out var item);

            return Task.FromResult((found, item));
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    private sealed class SpatialWorldServiceTestCharacterService : ICharacterService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _characters = [];

        public void Add(UOMobileEntity character)
            => _characters[character.Id] = character;

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(_characters.TryGetValue(characterId, out var character) ? character : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();
    }

    [Test]
    public void GetMusic_ShouldResolveMusicByRegionCoordinate()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);

        service.AddRegion(
            new()
            {
                Music = MusicName.Cove,
                Area = [new() { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        );

        var found = service.GetMusic(0, new(150, 150, 0));
        var otherMap = service.GetMusic(1, new(150, 150, 0));
        var fallback = service.GetMusic(0, new(5000, 5000, 0));

        Assert.That(found, Is.EqualTo((int)MusicName.Cove));
        Assert.That(otherMap, Is.EqualTo(0));
        Assert.That(fallback, Is.EqualTo(0));
    }

    [Test]
    public void AddOrUpdateItem_ShouldBeQueryableByMapAndRange()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var item = new UOItemEntity
        {
            Id = (Serial)0x200u,
            Location = new(250, 260, 0),
            ItemId = 0x0EED
        };

        service.AddOrUpdateItem(item, 1);
        var foundSameMap = service.GetNearbyItems(new(250, 260, 0), 3, 1);
        var foundOtherMap = service.GetNearbyItems(new(250, 260, 0), 3, 0);

        Assert.That(foundSameMap.Select(static value => value.Id), Contains.Item(item.Id));
        Assert.That(foundOtherMap, Is.Empty);
    }

    [Test]
    public void AddOrUpdateMobile_ShouldPublishMobileAddedInSectorEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x500, 10, 10, 0, false);

        service.AddOrUpdateMobile(mobile);

        var gameEvent = eventBus.Events.OfType<MobileAddedInSectorEvent>().Single();
        Assert.That(gameEvent.MobileId, Is.EqualTo(mobile.Id));
    }

    [Test]
    public void AddOrUpdateMobile_ShouldReturnMobileInRange()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x100, 100, 100, 0, false);

        service.AddOrUpdateMobile(mobile);
        var result = service.GetNearbyMobiles(new(100, 100, 0), 5, 0);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(mobile.Id));
    }

    [Test]
    public void GetPlayersInRange_ShouldReturnMappedSessionsAndApplyExclusion()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var firstPlayer = CreateMobile(0x300, 500, 500, 0, true);
        var secondPlayer = CreateMobile(0x301, 502, 503, 0, true);

        service.AddOrUpdateMobile(firstPlayer);
        service.AddOrUpdateMobile(secondPlayer);

        var firstSession = CreateSession(firstPlayer);
        var secondSession = CreateSession(secondPlayer);
        sessions.Add(firstSession);
        sessions.Add(secondSession);

        var included = service.GetPlayersInRange(new(500, 500, 0), 8, 0);
        var excluded = service.GetPlayersInRange(new(500, 500, 0), 8, 0, firstSession);

        Assert.That(included.Select(static session => session.SessionId), Contains.Item(firstSession.SessionId));
        Assert.That(included.Select(static session => session.SessionId), Contains.Item(secondSession.SessionId));
        Assert.That(excluded.Select(static session => session.SessionId), Does.Not.Contain(firstSession.SessionId));
        Assert.That(excluded.Select(static session => session.SessionId), Contains.Item(secondSession.SessionId));
    }

    [Test]
    public void GetPlayersInSector_ShouldReturnOnlyPlayersInTargetSector()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);

        var playerInSector = CreateMobile(0x310, 140, 150, 0, true);
        var npcInSector = CreateMobile(0x311, 141, 151, 0, false);
        var playerOtherSector = CreateMobile(0x312, 800, 800, 0, true);

        service.AddOrUpdateMobile(playerInSector);
        service.AddOrUpdateMobile(npcInSector);
        service.AddOrUpdateMobile(playerOtherSector);

        var sectorX = playerInSector.Location.X >> MapSectorConsts.SectorShift;
        var sectorY = playerInSector.Location.Y >> MapSectorConsts.SectorShift;
        var players = service.GetPlayersInSector(0, sectorX, sectorY);

        Assert.Multiple(
            () =>
            {
                Assert.That(players.Select(static player => player.Id), Contains.Item(playerInSector.Id));
                Assert.That(players.Select(static player => player.Id), Does.Not.Contain(npcInSector.Id));
                Assert.That(players.Select(static player => player.Id), Does.Not.Contain(playerOtherSector.Id));
            }
        );
    }

    [Test]
    public void GetActiveSectors_ShouldReturnLoadedSectorsAcrossMaps()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);

        service.AddOrUpdateMobile(CreateMobile(0x320, 16, 16, 0, true));
        service.AddOrUpdateItem(
            new UOItemEntity
            {
                Id = (Serial)0x900u,
                ItemId = 0x0EED,
                Location = new(32, 32, 0)
            },
            1
        );

        var sectors = service.GetActiveSectors();

        Assert.Multiple(
            () =>
            {
                Assert.That(sectors.Any(static sector => sector.MapIndex == 0 && sector.SectorX == 1 && sector.SectorY == 1), Is.True);
                Assert.That(sectors.Any(static sector => sector.MapIndex == 1 && sector.SectorX == 2 && sector.SectorY == 2), Is.True);
            }
        );
    }

    [Test]
    public void GetSectorByLocation_ShouldLazyLoadGroundItemsForMissingSector()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new SpatialWorldServiceTestItemService();
        var service = CreateService(sessions, eventBus, itemService);
        var location = new Point3D(130, 130, 0);
        var expectedItem = new UOItemEntity
        {
            Id = (Serial)0x700u,
            ItemId = 0x0EED,
            Location = location
        };
        itemService.ItemsBySector[(0, 8, 8)] = [expectedItem];

        var sector = service.GetSectorByLocation(0, location);

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector!.GetItems().Select(static item => item.Id), Contains.Item(expectedItem.Id));
        Assert.That(itemService.LoadRequests, Has.Member((0, 8, 8)));
    }

    [Test]
    public void GetSectorByLocation_ShouldReturnMatchingSector_WhenIndexed()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x320, 300, 400, 0, true);

        service.AddOrUpdateMobile(mobile);

        var sector = service.GetSectorByLocation(0, mobile.Location);

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector!.GetPlayers().Select(static player => player.Id), Contains.Item(mobile.Id));
    }

    [Test]
    public void GetSectorByLocation_ShouldReturnNull_WhenMapNotIndexed()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);

        var sector = service.GetSectorByLocation(99, new(10, 10, 0));

        Assert.That(sector, Is.Null);
    }

    [Test]
    public async Task HandleAsync_DropItemToGround_ShouldUseRuntimeSessionMapAndNewLocation()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new SpatialWorldServiceTestItemService();
        var characterService = new SpatialWorldServiceTestCharacterService();
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var service = CreateService(
            sessions,
            eventBus,
            itemService,
            characterService,
            new() { SectorWarmupRadius = 0, LazySectorItemLoadEnabled = false },
            queue
        );

        var player = CreateMobile(0x710, 3465, 2592, 1, true);
        var staleCharacter = CreateMobile(0x710, 100, 100, 1, true);
        characterService.Add(staleCharacter);

        service.AddOrUpdateMobile(player);
        var session = CreateSession(player);
        sessions.Add(session);

        var droppedItem = new UOItemEntity
        {
            Id = (Serial)0x711u,
            ItemId = 0x0EED,
            Location = new(3465, 2592, 14),
            MapId = 1
        };
        itemService.ItemsById[droppedItem.Id] = droppedItem;

        await service.HandleAsync(
            new DropItemToGroundEvent(
                session.SessionId,
                player.Id,
                droppedItem.Id,
                (Serial)0x40000001u,
                new(3466, 2592, 14),
                droppedItem.Location
            )
        );

        Assert.That(queue.CurrentQueueDepth, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_PlayerCharacterLoggedIn_ShouldWarmupSectorsAroundPlayer()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var itemService = new SpatialWorldServiceTestItemService();
        var characterService = new SpatialWorldServiceTestCharacterService();
        var service = CreateService(
            sessions,
            eventBus,
            itemService,
            characterService,
            new() { SectorWarmupRadius = 1, LazySectorItemLoadEnabled = true }
        );
        var character = CreateMobile(0x701, 130, 130, 0, true);
        characterService.Add(character);

        await service.HandleAsync(new PlayerCharacterLoggedInEvent(1, (Serial)0x01u, character.Id));

        Assert.That(itemService.LoadRequests.Distinct().Count(), Is.EqualTo(9));
        Assert.That(itemService.LoadRequests, Has.Member((0, 8, 8)));
        Assert.That(itemService.LoadRequests, Has.Member((0, 7, 7)));
        Assert.That(itemService.LoadRequests, Has.Member((0, 9, 9)));
    }

    [Test]
    public async Task HandleAsync_PlayerCharacterLoggedIn_InRegion_ShouldPublishPlayerEnteredRegionEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var characterService = new SpatialWorldServiceTestCharacterService();
        var service = CreateService(sessions, eventBus, characterService: characterService);
        var character = CreateMobile(0x702, 130, 130, 0, true);
        characterService.Add(character);
        service.AddRegion(
            new()
            {
                Id = 200,
                Name = "Yew",
                Area = [new() { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        );

        await service.HandleAsync(new PlayerCharacterLoggedInEvent(1, (Serial)0x01u, character.Id));

        var enteredEvent = eventBus.Events.OfType<PlayerEnteredRegionEvent>().Single();
        Assert.Multiple(
            () =>
            {
                Assert.That(enteredEvent.MobileId, Is.EqualTo(character.Id));
                Assert.That(enteredEvent.MapId, Is.EqualTo(character.MapId));
                Assert.That(enteredEvent.RegionId, Is.EqualTo(200));
                Assert.That(enteredEvent.RegionName, Is.EqualTo("Yew"));
            }
        );
    }

    [Test]
    public void OnMobileMoved_ShouldUpdateRangeQueriesAcrossSectors()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x101, 10, 10, 0, false);

        service.AddOrUpdateMobile(mobile);
        service.OnMobileMoved(mobile, new(10, 10, 0), new(200, 200, 0));

        var oldLocationResult = service.GetNearbyMobiles(new(10, 10, 0), 4, 0);
        var newLocationResult = service.GetNearbyMobiles(new(200, 200, 0), 4, 0);

        Assert.That(oldLocationResult, Is.Empty);
        Assert.That(newLocationResult.Select(static item => item.Id), Contains.Item(mobile.Id));
    }

    [Test]
    public void OnMobileMoved_WhenSectorChanges_ShouldPublishMobileSectorChangedEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x501, 1, 1, 0, false);

        service.AddOrUpdateMobile(mobile);
        service.OnMobileMoved(mobile, new(1, 1, 0), new(200, 200, 0));

        var gameEvent = eventBus.Events.OfType<MobileSectorChangedEvent>().Single();
        Assert.Multiple(
            () =>
            {
                Assert.That(gameEvent.MobileId, Is.EqualTo(mobile.Id));
                Assert.That(gameEvent.OldSectorX, Is.Not.EqualTo(gameEvent.NewSectorX));
                Assert.That(gameEvent.OldSectorY, Is.Not.EqualTo(gameEvent.NewSectorY));
            }
        );
    }

    [Test]
    public void OnMobileMoved_WhenSectorDoesNotChange_ShouldNotPublishMobileSectorChangedEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x502, 20, 20, 0, false);

        service.AddOrUpdateMobile(mobile);
        eventBus.Events.Clear();
        service.OnMobileMoved(mobile, new(20, 20, 0), new(25, 25, 0));

        Assert.That(eventBus.Events.OfType<MobileSectorChangedEvent>(), Is.Empty);
    }

    [Test]
    public void OnMobileMoved_ForPlayer_WhenEnteringRegion_ShouldPublishPlayerEnteredRegionEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var player = CreateMobile(0x601, 10, 10, 0, true);
        service.AddRegion(
            new()
            {
                Id = 100,
                Name = "Britain",
                Area = [new() { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        );

        service.AddOrUpdateMobile(player);
        eventBus.Events.Clear();
        service.OnMobileMoved(player, new(10, 10, 0), new(120, 120, 0));

        var enteredEvent = eventBus.Events.OfType<PlayerEnteredRegionEvent>().Single();
        Assert.Multiple(
            () =>
            {
                Assert.That(enteredEvent.MobileId, Is.EqualTo(player.Id));
                Assert.That(enteredEvent.MapId, Is.EqualTo(player.MapId));
                Assert.That(enteredEvent.RegionId, Is.EqualTo(100));
                Assert.That(enteredEvent.RegionName, Is.EqualTo("Britain"));
            }
        );
    }

    [Test]
    public void OnMobileMoved_ForPlayer_WhenExitingRegion_ShouldPublishPlayerExitedRegionEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var player = CreateMobile(0x602, 120, 120, 0, true);
        service.AddRegion(
            new()
            {
                Id = 101,
                Name = "Moonglow",
                Area = [new() { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        );

        service.AddOrUpdateMobile(player);
        eventBus.Events.Clear();
        service.OnMobileMoved(player, new(120, 120, 0), new(10, 10, 0));

        var exitedEvent = eventBus.Events.OfType<PlayerExitedRegionEvent>().Single();
        Assert.Multiple(
            () =>
            {
                Assert.That(exitedEvent.MobileId, Is.EqualTo(player.Id));
                Assert.That(exitedEvent.MapId, Is.EqualTo(player.MapId));
                Assert.That(exitedEvent.RegionId, Is.EqualTo(101));
                Assert.That(exitedEvent.RegionName, Is.EqualTo("Moonglow"));
            }
        );
    }

    [Test]
    public void RemoveEntity_ShouldRemoveMobileAndItemFromQueries()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x400, 1200, 1200, 0, false);
        var item = new UOItemEntity { Id = (Serial)0x401u, ItemId = 0x0EED, Location = new(1200, 1200, 0) };

        service.AddOrUpdateMobile(mobile);
        service.AddOrUpdateItem(item, 0);

        service.RemoveEntity(mobile.Id);
        service.RemoveEntity(item.Id);

        var mobileResult = service.GetNearbyMobiles(new(1200, 1200, 0), 5, 0);
        var itemResult = service.GetNearbyItems(new(1200, 1200, 0), 5, 0);

        Assert.That(mobileResult.Select(static value => value.Id), Does.Not.Contain(mobile.Id));
        Assert.That(itemResult.Select(static value => value.Id), Does.Not.Contain(item.Id));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clientsToDispose)
        {
            client.Dispose();
        }

        _clientsToDispose.Clear();
    }

    private static UOMobileEntity CreateMobile(uint id, int x, int y, int mapId, bool isPlayer)
        => new()
        {
            Id = (Serial)id,
            Name = $"mobile_{id}",
            Location = new(x, y, 0),
            MapId = mapId,
            IsPlayer = isPlayer
        };

    private static SpatialWorldService CreateService(
        FakeGameNetworkSessionService sessions,
        NetworkServiceTestGameEventBusService eventBus,
        SpatialWorldServiceTestItemService? itemService = null,
        ICharacterService? characterService = null,
        MoongateSpatialConfig? spatialConfig = null,
        BasePacketListenerTestOutgoingPacketQueue? queue = null
    )
    {
        var config = new MoongateConfig
        {
            Spatial = spatialConfig ?? new MoongateSpatialConfig()
        };

        return new(
            sessions,
            eventBus,
            characterService ?? new MovementHandlerTestCharacterService(),
            itemService ?? new SpatialWorldServiceTestItemService(),
            queue ?? new BasePacketListenerTestOutgoingPacketQueue(),
            config
        );
    }

    private GameSession CreateSession(UOMobileEntity character)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);
        var networkSession = new GameNetworkSession(client);

        return new(networkSession)
        {
            Character = character,
            CharacterId = character.Id,
            AccountType = AccountType.Regular
        };
    }
}
