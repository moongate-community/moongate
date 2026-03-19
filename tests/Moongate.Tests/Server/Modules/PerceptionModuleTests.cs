using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class PerceptionModuleTests
{
    private sealed class PerceptionTestSpatialWorldService : ISpatialWorldService
    {
        private readonly List<UOMobileEntity> _mobiles = [];
        private readonly Dictionary<(int MapId, int SectorX, int SectorY), MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            _mobiles.Add(mobile);
            var key = (
                          mobile.MapId,
                          mobile.Location.X >> MapSectorConsts.SectorShift,
                          mobile.Location.Y >> MapSectorConsts.SectorShift
                      );

            if (!_sectors.TryGetValue(key, out var sector))
            {
                sector = new(key.MapId, key.Item2, key.Item3);
                _sectors[key] = sector;
            }

            sector.AddEntity(mobile);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

        public void AddRegion(JsonRegion region) { }

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
            => _mobiles
               .Where(mobile => mobile.MapId == mapId && mobile.Location.InRange(location, range))
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
            => null;

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation) { }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation) { }

        public void RemoveEntity(Serial serial) { }
    }

    [Test]
    public void Distance_AndInRange_ShouldReturnExpectedValues()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var a = new UOMobileEntity { Id = (Serial)0x20u, MapId = 1, Location = new(100, 100, 0) };
        var b = new UOMobileEntity { Id = (Serial)0x21u, MapId = 1, Location = new(103, 104, 0) };
        spatial.AddMobile(a);
        spatial.AddMobile(b);
        var module = new PerceptionModule(spatial);

        var distance = module.Distance((uint)a.Id, (uint)b.Id);
        var inRange5 = module.InRange((uint)a.Id, (uint)b.Id, 5);
        var inRange4 = module.InRange((uint)a.Id, (uint)b.Id, 4);

        Assert.Multiple(
            () =>
            {
                Assert.That(distance, Is.EqualTo(5));
                Assert.That(inRange5, Is.True);
                Assert.That(inRange4, Is.False);
            }
        );
    }

    [Test]
    public void FindNearestEnemy_AndFriend_ShouldResolveClosestMobiles()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = new UOMobileEntity { Id = (Serial)0x10u, MapId = 1, Location = new(100, 100, 0) };
        var enemyNear = new UOMobileEntity { Id = (Serial)0x11u, IsPlayer = true, MapId = 1, Location = new(102, 100, 0) };
        var enemyFar = new UOMobileEntity { Id = (Serial)0x12u, IsPlayer = true, MapId = 1, Location = new(110, 100, 0) };
        var friendNear = new UOMobileEntity { Id = (Serial)0x13u, IsPlayer = false, MapId = 1, Location = new(101, 100, 0) };
        spatial.AddMobile(npc);
        spatial.AddMobile(enemyNear);
        spatial.AddMobile(enemyFar);
        spatial.AddMobile(friendNear);
        var module = new PerceptionModule(spatial);

        var nearestEnemy = module.FindNearestEnemy((uint)npc.Id, 20);
        var nearestFriend = module.FindNearestFriend((uint)npc.Id, 20);

        Assert.Multiple(
            () =>
            {
                Assert.That(nearestEnemy, Is.EqualTo((uint)enemyNear.Id));
                Assert.That(nearestFriend, Is.EqualTo((uint)friendNear.Id));
            }
        );
    }
}
