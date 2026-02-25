using System.Net;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Packets;
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

    [Test]
    public void AddOrUpdateMobile_ShouldReturnMobileInRange()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x100, 100, 100, mapId: 0, isPlayer: false);

        service.AddOrUpdateMobile(mobile);
        var result = service.GetNearbyMobiles(new Point3D(100, 100, 0), 5, 0);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(mobile.Id));
    }

    [Test]
    public void OnMobileMoved_ShouldUpdateRangeQueriesAcrossSectors()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x101, 10, 10, mapId: 0, isPlayer: false);

        service.AddOrUpdateMobile(mobile);
        service.OnMobileMoved(mobile, new Point3D(10, 10, 0), new Point3D(200, 200, 0));

        var oldLocationResult = service.GetNearbyMobiles(new Point3D(10, 10, 0), 4, 0);
        var newLocationResult = service.GetNearbyMobiles(new Point3D(200, 200, 0), 4, 0);

        Assert.That(oldLocationResult, Is.Empty);
        Assert.That(newLocationResult.Select(static item => item.Id), Contains.Item(mobile.Id));
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
            Location = new Point3D(250, 260, 0),
            ItemId = 0x0EED
        };

        service.AddOrUpdateItem(item, mapId: 1);
        var foundSameMap = service.GetNearbyItems(new Point3D(250, 260, 0), 3, 1);
        var foundOtherMap = service.GetNearbyItems(new Point3D(250, 260, 0), 3, 0);

        Assert.That(foundSameMap.Select(static value => value.Id), Contains.Item(item.Id));
        Assert.That(foundOtherMap, Is.Empty);
    }

    [Test]
    public void GetPlayersInRange_ShouldReturnMappedSessionsAndApplyExclusion()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var firstPlayer = CreateMobile(0x300, 500, 500, mapId: 0, isPlayer: true);
        var secondPlayer = CreateMobile(0x301, 502, 503, mapId: 0, isPlayer: true);

        service.AddOrUpdateMobile(firstPlayer);
        service.AddOrUpdateMobile(secondPlayer);

        var firstSession = CreateSession(firstPlayer);
        var secondSession = CreateSession(secondPlayer);
        sessions.Add(firstSession);
        sessions.Add(secondSession);

        var included = service.GetPlayersInRange(new Point3D(500, 500, 0), 8, 0);
        var excluded = service.GetPlayersInRange(new Point3D(500, 500, 0), 8, 0, firstSession);

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

        var playerInSector = CreateMobile(0x310, 140, 150, mapId: 0, isPlayer: true);
        var npcInSector = CreateMobile(0x311, 141, 151, mapId: 0, isPlayer: false);
        var playerOtherSector = CreateMobile(0x312, 800, 800, mapId: 0, isPlayer: true);

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
    public void GetSectorByLocation_ShouldReturnMatchingSector_WhenIndexed()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x320, 300, 400, mapId: 0, isPlayer: true);

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

        var sector = service.GetSectorByLocation(99, new Point3D(10, 10, 0));

        Assert.That(sector, Is.Null);
    }

    [Test]
    public void RemoveEntity_ShouldRemoveMobileAndItemFromQueries()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x400, 1200, 1200, mapId: 0, isPlayer: false);
        var item = new UOItemEntity { Id = (Serial)0x401u, ItemId = 0x0EED, Location = new Point3D(1200, 1200, 0) };

        service.AddOrUpdateMobile(mobile);
        service.AddOrUpdateItem(item, mapId: 0);

        service.RemoveEntity(mobile.Id);
        service.RemoveEntity(item.Id);

        var mobileResult = service.GetNearbyMobiles(new Point3D(1200, 1200, 0), 5, 0);
        var itemResult = service.GetNearbyItems(new Point3D(1200, 1200, 0), 5, 0);

        Assert.That(mobileResult.Select(static value => value.Id), Does.Not.Contain(mobile.Id));
        Assert.That(itemResult.Select(static value => value.Id), Does.Not.Contain(item.Id));
    }

    [Test]
    public void AddOrUpdateMobile_ShouldPublishMobileAddedInSectorEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x500, 10, 10, mapId: 0, isPlayer: false);

        service.AddOrUpdateMobile(mobile);

        var gameEvent = eventBus.Events.OfType<MobileAddedInSectorEvent>().Single();
        Assert.That(gameEvent.MobileId, Is.EqualTo(mobile.Id));
    }

    [Test]
    public void OnMobileMoved_WhenSectorChanges_ShouldPublishMobileSectorChangedEvent()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);
        var mobile = CreateMobile(0x501, 1, 1, mapId: 0, isPlayer: false);

        service.AddOrUpdateMobile(mobile);
        service.OnMobileMoved(mobile, new Point3D(1, 1, 0), new Point3D(200, 200, 0));

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
        var mobile = CreateMobile(0x502, 20, 20, mapId: 0, isPlayer: false);

        service.AddOrUpdateMobile(mobile);
        eventBus.Events.Clear();
        service.OnMobileMoved(mobile, new Point3D(20, 20, 0), new Point3D(25, 25, 0));

        Assert.That(eventBus.Events.OfType<MobileSectorChangedEvent>(), Is.Empty);
    }

    [Test]
    public void AddMusicsAndGetMusic_ShouldResolveMusicByRegionCoordinate()
    {
        var sessions = new FakeGameNetworkSessionService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var service = CreateService(sessions, eventBus);

        service.AddRegion(
            new Moongate.UO.Data.Json.Regions.JsonRegion
            {
                MusicList = 7,
                Coordinates = [new Moongate.UO.Data.Json.JsonCoordinate { X1 = 100, Y1 = 100, X2 = 200, Y2 = 200 }]
            }
        );
        service.AddMusics([new Moongate.UO.Data.Json.Regions.JsonMusic { Id = 7, Name = "Town", Music = 42 }]);

        var found = service.GetMusic(new Point3D(150, 150, 0));
        var fallback = service.GetMusic(new Point3D(5000, 5000, 0));

        Assert.That(found, Is.EqualTo(42));
        Assert.That(fallback, Is.EqualTo(0));
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
            new MoongateSpatialConfig { SectorWarmupRadius = 1, LazySectorItemLoadEnabled = true }
        );
        var character = CreateMobile(0x701, 130, 130, mapId: 0, isPlayer: true);
        characterService.Add(character);

        await service.HandleAsync(new PlayerCharacterLoggedInEvent(1, (Serial)0x01u, character.Id));

        Assert.That(itemService.LoadRequests.Distinct().Count(), Is.EqualTo(9));
        Assert.That(itemService.LoadRequests, Has.Member((0, 8, 8)));
        Assert.That(itemService.LoadRequests, Has.Member((0, 7, 7)));
        Assert.That(itemService.LoadRequests, Has.Member((0, 9, 9)));
    }

    private static SpatialWorldService CreateService(
        FakeGameNetworkSessionService sessions,
        NetworkServiceTestGameEventBusService eventBus,
        SpatialWorldServiceTestItemService? itemService = null,
        ICharacterService? characterService = null,
        MoongateSpatialConfig? spatialConfig = null
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
            new BasePacketListenerTestOutgoingPacketQueue(),
            config
        );
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

    private GameSession CreateSession(UOMobileEntity character)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);
        var networkSession = new GameNetworkSession(client);

        return new GameSession(networkSession)
        {
            Character = character,
            CharacterId = character.Id,
            AccountType = AccountType.Regular
        };
    }

    private static UOMobileEntity CreateMobile(uint id, int x, int y, int mapId, bool isPlayer)
    {
        return new UOMobileEntity
        {
            Id = (Serial)id,
            Name = $"mobile_{id}",
            Location = new Point3D(x, y, 0),
            MapId = mapId,
            IsPlayer = isPlayer
        };
    }

    private sealed class SpatialWorldServiceTestItemService : IItemService
    {
        public Dictionary<(int MapId, int SectorX, int SectorY), List<UOItemEntity>> ItemsBySector { get; } = [];

        public List<(int MapId, int SectorX, int SectorY)> LoadRequests { get; } = [];

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
        {
            LoadRequests.Add((mapId, sectorX, sectorY));
            ItemsBySector.TryGetValue((mapId, sectorX, sectorY), out var items);
            return Task.FromResult(items ?? []);
        }

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(Serial itemId, Point3D location, int mapId)
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

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
            => throw new NotSupportedException();

        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
            => throw new NotSupportedException();

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
            => Task.FromResult(_characters.TryGetValue(characterId, out var character) ? character : null);

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
            => throw new NotSupportedException();
    }
}
