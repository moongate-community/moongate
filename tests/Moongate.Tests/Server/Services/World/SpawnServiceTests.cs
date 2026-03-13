using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.World;

public sealed class SpawnServiceTests
{
    private readonly List<MoongateTCPClient> _clientsToDispose = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var client in _clientsToDispose)
        {
            client.Dispose();
        }

        _clientsToDispose.Clear();
    }

    [Test]
    public async Task StartAsync_WhenDefinitionIsProximitySpawner_ShouldSpawnOnlyOnPlayerEnter()
    {
        var definition = CreateSpawnDefinition(SpawnDefinitionKind.ProximitySpawner, homeRange: 5);
        var timer = new SpawnServiceTestTimerService();
        var spatial = new SpawnServiceTestSpatialWorldService();
        var mobileService = new SpawnServiceTestMobileService();
        var service = CreateService(timer, spatial, mobileService, [definition]);
        var spawnerItem = CreateSpawnerItem(definition.Guid);

        spatial.ActiveSectors.Add(CreateSectorWithItem(spawnerItem));

        await service.StartAsync();

        timer.Fire();
        Assert.That(mobileService.SpawnAttempts, Is.EqualTo(0));

        spatial.PlayersInRange = [CreateSession((Serial)0x00000033u)];

        timer.Fire();
        Assert.That(mobileService.SpawnAttempts, Is.EqualTo(1));

        timer.Fire();
        Assert.That(mobileService.SpawnAttempts, Is.EqualTo(1));
    }

    [Test]
    public async Task TriggerAsync_WhenDefinitionIsProximitySpawner_ShouldStillForceSpawn()
    {
        var definition = CreateSpawnDefinition(SpawnDefinitionKind.ProximitySpawner, homeRange: 5);
        var timer = new SpawnServiceTestTimerService();
        var spatial = new SpawnServiceTestSpatialWorldService();
        var mobileService = new SpawnServiceTestMobileService();
        var service = CreateService(timer, spatial, mobileService, [definition]);
        var spawnerItem = CreateSpawnerItem(definition.Guid);

        await service.StartAsync();

        var result = await service.TriggerAsync(spawnerItem);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(mobileService.SpawnAttempts, Is.EqualTo(1));
            }
        );
    }

    private SpawnService CreateService(
        ITimerService timerService,
        ISpatialWorldService spatialWorldService,
        IMobileService mobileService,
        IReadOnlyList<SpawnDefinitionEntry> definitions
    )
        => new(
            timerService,
            spatialWorldService,
            new SpawnServiceTestSpawnsDataService(definitions),
            mobileService,
            new SpawnServiceTestMobileTemplateService(),
            new SpawnServiceTestGameEventBusService()
        );

    private GameSession CreateSession(Serial characterId)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var client = new MoongateTCPClient(socket);
        _clientsToDispose.Add(client);
        var networkSession = new GameNetworkSession(client);

        return new(networkSession)
        {
            CharacterId = characterId
        };
    }

    private static MapSector CreateSectorWithItem(UOItemEntity item)
    {
        var sector = new MapSector(0, 0, 0);
        sector.AddEntity(item);

        return sector;
    }

    private static UOItemEntity CreateSpawnerItem(Guid guid)
    {
        var item = new UOItemEntity
        {
            Id = (Serial)0x40000001u,
            MapId = 0,
            ItemId = 0x1F13,
            Name = "Spawner",
            Location = new Point3D(100, 100, 0)
        };
        item.SetCustomString("spawner_id", guid.ToString("D"));

        return item;
    }

    private static SpawnDefinitionEntry CreateSpawnDefinition(SpawnDefinitionKind kind, int homeRange)
        => new(
            0,
            "Felucca",
            "shared/felucca",
            "test.json",
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            kind,
            "Test Spawner",
            new Point3D(100, 100, 0),
            1,
            TimeSpan.Zero,
            TimeSpan.Zero,
            0,
            homeRange,
            homeRange,
            [new SpawnEntryDefinition("rat", 1, 100)]
        );

    private sealed class SpawnServiceTestTimerService : ITimerService
    {
        private Action? _callback;

        public void Fire()
            => _callback?.Invoke();

        public void ProcessTick() { }

        public string RegisterTimer(string name, TimeSpan interval, Action callback, TimeSpan? delay = null, bool repeat = false)
        {
            _callback = callback;
            return "spawn-service-test";
        }

        public void UnregisterAllTimers()
            => _callback = null;

        public bool UnregisterTimer(string timerId)
        {
            _callback = null;
            return true;
        }

        public int UnregisterTimersByName(string name)
        {
            _callback = null;
            return 1;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
            => 0;
    }

    private sealed class SpawnServiceTestSpatialWorldService : ISpatialWorldService
    {
        public List<MapSector> ActiveSectors { get; } = [];

        public List<GameSession> PlayersInRange { get; set; } = [];

        public List<UOMobileEntity> NearbyMobiles { get; set; } = [];

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile) { }

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet, int mapId, Point3D location, int? range = null, long? excludeSessionId = null)
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => [..ActiveSectors];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => [..NearbyMobiles];

        public List<GameSession> GetPlayersInRange(Point3D location, int range, int mapId, GameSession? excludeSession = null)
            => [..PlayersInRange];

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

    private sealed class SpawnServiceTestMobileService : IMobileService
    {
        public int SpawnAttempts { get; private set; }

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult<UOMobileEntity?>(null);

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(int mapId, int sectorX, int sectorY, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
        {
            SpawnAttempts++;
            return Task.FromResult(CreateMobile(location, mapId));
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(string templateId, Point3D location, int mapId, Serial? accountId = null, CancellationToken cancellationToken = default)
        {
            SpawnAttempts++;
            return Task.FromResult<(bool Spawned, UOMobileEntity? Mobile)>((true, CreateMobile(location, mapId)));
        }

        private static UOMobileEntity CreateMobile(Point3D location, int mapId)
            => new()
            {
                Id = (Serial)0x00000044u,
                Name = "rat",
                Location = location,
                MapId = mapId
            };
    }

    private sealed class SpawnServiceTestMobileTemplateService : IMobileTemplateService
    {
        public int Count => 0;

        public void Clear() { }

        public IReadOnlyList<MobileTemplateDefinition> GetAll()
            => [];

        public bool TryGet(string id, out MobileTemplateDefinition? definition)
        {
            definition = null;
            return false;
        }

        public void Upsert(MobileTemplateDefinition definition) { }

        public void UpsertRange(IEnumerable<MobileTemplateDefinition> definitions) { }
    }

    private sealed class SpawnServiceTestGameEventBusService : IGameEventBusService
    {
        public ValueTask PublishAsync<TEvent>(TEvent gameEvent, CancellationToken cancellationToken = default) where TEvent : Moongate.Server.Data.Events.Base.IGameEvent
            => ValueTask.CompletedTask;

        public void RegisterListener<TEvent>(IGameEventListener<TEvent> listener) where TEvent : Moongate.Server.Data.Events.Base.IGameEvent { }
    }

    private sealed class SpawnServiceTestSpawnsDataService : ISpawnsDataService
    {
        private readonly IReadOnlyList<SpawnDefinitionEntry> _definitions;

        public SpawnServiceTestSpawnsDataService(IReadOnlyList<SpawnDefinitionEntry> definitions)
        {
            _definitions = definitions;
        }

        public IReadOnlyList<SpawnDefinitionEntry> GetAllEntries()
            => _definitions;

        public IReadOnlyList<SpawnDefinitionEntry> GetEntriesByMap(int mapId)
            => [.._definitions.Where(entry => entry.MapId == mapId)];

        public void SetEntries(IReadOnlyList<SpawnDefinitionEntry> entries)
            => throw new NotSupportedException();
    }
}
