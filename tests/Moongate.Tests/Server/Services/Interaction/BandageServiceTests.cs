using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class BandageServiceTests
{
    private sealed class TimerServiceSpy : ITimerService
    {
        public sealed record RegisteredTimer(string Id, string Name, TimeSpan Interval, TimeSpan? Delay, Action Callback);

        private int _nextId;

        public List<RegisteredTimer> RegisteredTimers { get; } = [];

        public void ProcessTick() { }

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            _ = repeat;

            var timer = new RegisteredTimer($"timer-{++_nextId}", name, interval, delay, callback);
            RegisteredTimers.Add(timer);

            return timer.Id;
        }

        public void UnregisterAllTimers()
            => RegisteredTimers.Clear();

        public bool UnregisterTimer(string timerId)
            => RegisteredTimers.RemoveAll(timer => timer.Id == timerId) > 0;

        public int UnregisterTimersByName(string name)
            => RegisteredTimers.RemoveAll(timer => timer.Name == name);

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    private sealed class InMemoryMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = new();

        public int CreateOrUpdateCalls { get; private set; }

        public void Add(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CreateOrUpdateCalls++;
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

    private sealed class InMemoryItemService : IItemService
    {
        private readonly Dictionary<Serial, UOItemEntity> _items = new();

        public int UpsertCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void Add(UOItemEntity item)
            => _items[item.Id] = item;

        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => Task.CompletedTask;

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => Task.FromException<UOItemEntity?>(new NotSupportedException());

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => Task.FromException<Serial>(new NotSupportedException());

        public Task<bool> DeleteItemAsync(Serial itemId)
        {
            DeleteCalls++;

            return Task.FromResult(_items.Remove(itemId));
        }

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
            => Task.FromResult(_items.TryGetValue(itemId, out var item) ? (true, item) : (false, (UOItemEntity?)null));

        public Task UpsertItemAsync(UOItemEntity item)
        {
            UpsertCalls++;
            _items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => Task.CompletedTask;
    }

    private sealed class SpatialWorldServiceSpy : ISpatialWorldService
    {
        private readonly Dictionary<(int MapId, int SectorX, int SectorY), MapSector> _sectors = new();

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
            => _ = (item, mapId);

        public void AddOrUpdateMobile(UOMobileEntity mobile)
        {
            var sectorX = mobile.Location.X >> MapSectorConsts.SectorShift;
            var sectorY = mobile.Location.Y >> MapSectorConsts.SectorShift;
            var key = (mobile.MapId, sectorX, sectorY);

            if (!_sectors.TryGetValue(key, out var sector))
            {
                sector = new(mobile.MapId, sectorX, sectorY);
                _sectors[key] = sector;
            }

            sector.AddEntity(mobile);
        }

        public void AddRegion(JsonRegion region)
            => _ = region;

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
            => Task.FromResult(0);

        public List<MapSector> GetActiveSectors()
            => [.. _sectors.Values];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
            => [];

        public int GetMusic(int mapId, Point3D location)
            => 0;

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
            => [];

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
            => _sectors.Values
                       .Where(sector => sector.MapIndex == mapId)
                       .SelectMany(sector => sector.GetMobiles())
                       .Where(mobile => mobile.Location.InRange(location, range))
                       .ToList();

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
        {
            var key = (mapId, location.X >> MapSectorConsts.SectorShift, location.Y >> MapSectorConsts.SectorShift);

            return _sectors.GetValueOrDefault(key);
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    [Test]
    public async Task BeginSelfBandageAsync_WhenAlreadyBandaging_ShouldReturnFalse()
    {
        var timerService = new TimerServiceSpy();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var spatialWorldService = new SpatialWorldServiceSpy();
        var service = new BandageService(timerService, mobileService, itemService, spatialWorldService);
        var mobile = CreateMobileWithBackpack();
        var bandage = CreateBandage((Serial)0x720u, 5);
        var backpack = mobile.GetEquippedItemsRuntime().Single();

        backpack.AddItem(bandage, new(10, 10));
        mobileService.Add(mobile);
        itemService.Add(bandage);
        spatialWorldService.AddOrUpdateMobile(mobile);

        var firstStart = await service.BeginSelfBandageAsync(mobile.Id);
        var secondStart = await service.BeginSelfBandageAsync(mobile.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(firstStart, Is.True);
                Assert.That(secondStart, Is.False);
                Assert.That(bandage.Amount, Is.EqualTo(4));
                Assert.That(timerService.RegisteredTimers.Count, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task BeginSelfBandageAsync_WhenBandageExists_ShouldConsumeOneAndRegisterTimer()
    {
        var timerService = new TimerServiceSpy();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var spatialWorldService = new SpatialWorldServiceSpy();
        var service = new BandageService(timerService, mobileService, itemService, spatialWorldService);
        var mobile = CreateMobileWithBackpack();
        var bandage = CreateBandage((Serial)0x710u, 5);
        var backpack = mobile.GetEquippedItemsRuntime().Single();

        backpack.AddItem(bandage, new(10, 10));
        mobileService.Add(mobile);
        itemService.Add(bandage);
        spatialWorldService.AddOrUpdateMobile(mobile);

        var started = await service.BeginSelfBandageAsync(mobile.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(started, Is.True);
                Assert.That(service.IsBandaging(mobile.Id), Is.True);
                Assert.That(bandage.Amount, Is.EqualTo(4));
                Assert.That(itemService.UpsertCalls, Is.EqualTo(1));
                Assert.That(timerService.RegisteredTimers.Count, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task BeginSelfBandageAsync_WhenNoBandageExists_ShouldReturnFalse()
    {
        var timerService = new TimerServiceSpy();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var spatialWorldService = new SpatialWorldServiceSpy();
        var service = new BandageService(timerService, mobileService, itemService, spatialWorldService);
        var mobile = CreateMobileWithBackpack();

        mobileService.Add(mobile);
        spatialWorldService.AddOrUpdateMobile(mobile);

        var started = await service.BeginSelfBandageAsync(mobile.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(started, Is.False);
                Assert.That(service.IsBandaging(mobile.Id), Is.False);
                Assert.That(timerService.RegisteredTimers, Is.Empty);
            }
        );
    }

    [Test]
    public async Task RegisteredTimerCallback_WhenMobileAlive_ShouldHealAndClearInFlightState()
    {
        var timerService = new TimerServiceSpy();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var spatialWorldService = new SpatialWorldServiceSpy();
        var service = new BandageService(timerService, mobileService, itemService, spatialWorldService);
        var mobile = CreateMobileWithBackpack();
        var bandage = CreateBandage((Serial)0x730u, 1);
        var backpack = mobile.GetEquippedItemsRuntime().Single();

        mobile.Hits = 30;
        backpack.AddItem(bandage, new(10, 10));
        mobileService.Add(mobile);
        itemService.Add(bandage);
        spatialWorldService.AddOrUpdateMobile(mobile);

        _ = await service.BeginSelfBandageAsync(mobile.Id);
        timerService.RegisteredTimers.Single().Callback();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Hits, Is.EqualTo(42));
                Assert.That(service.IsBandaging(mobile.Id), Is.False);
                Assert.That(itemService.DeleteCalls, Is.EqualTo(1));
                Assert.That(mobileService.CreateOrUpdateCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public async Task RegisteredTimerCallback_WhenMobileDead_ShouldNotHealButShouldClearInFlightState()
    {
        var timerService = new TimerServiceSpy();
        var mobileService = new InMemoryMobileService();
        var itemService = new InMemoryItemService();
        var spatialWorldService = new SpatialWorldServiceSpy();
        var service = new BandageService(timerService, mobileService, itemService, spatialWorldService);
        var mobile = CreateMobileWithBackpack();
        var bandage = CreateBandage((Serial)0x740u, 1);
        var backpack = mobile.GetEquippedItemsRuntime().Single();

        mobile.Hits = 30;
        backpack.AddItem(bandage, new(10, 10));
        mobileService.Add(mobile);
        itemService.Add(bandage);
        spatialWorldService.AddOrUpdateMobile(mobile);

        _ = await service.BeginSelfBandageAsync(mobile.Id);
        mobile.Hits = 0;
        mobile.IsAlive = false;
        timerService.RegisteredTimers.Single().Callback();

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Hits, Is.EqualTo(0));
                Assert.That(service.IsBandaging(mobile.Id), Is.False);
                Assert.That(mobileService.CreateOrUpdateCalls, Is.EqualTo(0));
            }
        );
    }

    private static UOItemEntity CreateBandage(Serial id, int amount)
        => new()
        {
            Id = id,
            ItemId = 0x0E21,
            Name = "Bandage",
            IsStackable = true,
            Amount = amount,
            Weight = 1
        };

    private static UOMobileEntity CreateMobileWithBackpack()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x700u,
            MapId = 1,
            Location = new(100, 100, 0),
            Hits = 50,
            MaxHits = 100,
            IsAlive = true,
            Name = "guard"
        };
        var backpack = new UOItemEntity
        {
            Id = (Serial)0x701u,
            ItemId = 0x0E75,
            Name = "Backpack",
            IsStackable = false
        };

        mobile.AddEquippedItem(ItemLayerType.Backpack, backpack);
        mobile.BackpackId = backpack.Id;

        return mobile;
    }
}
