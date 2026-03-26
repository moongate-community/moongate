using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Stat = Moongate.UO.Data.Types.Stat;

namespace Moongate.Tests.Server.Modules;

public sealed class PerceptionModuleTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                (int)UOSkillName.Tactics,
                "Tactics",
                0,
                100,
                0,
                "Warrior",
                0,
                0,
                0,
                1,
                "Tactics",
                Stat.Strength,
                Stat.Dexterity
            )
        ];

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
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x10u,
            MapId = 1,
            Location = new(100, 100, 0),
            BaseBody = 0x0003,
            Notoriety = Notoriety.CanBeAttacked
        };
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

    [Test]
    public void FindNearestEnemy_ShouldTreatHostileNpcAsEnemyCandidate()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = new UOMobileEntity { Id = (Serial)0x30u, MapId = 1, Location = new(100, 100, 0) };
        var hostileNpc = new UOMobileEntity
        {
            Id = (Serial)0x31u,
            IsPlayer = false,
            Notoriety = Notoriety.CanBeAttacked,
            MapId = 1,
            Location = new(101, 100, 0)
        };
        var playerFar = new UOMobileEntity
        {
            Id = (Serial)0x32u,
            IsPlayer = true,
            MapId = 1,
            Location = new(110, 100, 0)
        };
        spatial.AddMobile(npc);
        spatial.AddMobile(hostileNpc);
        spatial.AddMobile(playerFar);
        var module = new PerceptionModule(spatial);

        var nearestEnemy = module.FindNearestEnemy((uint)npc.Id, 20);

        Assert.That(nearestEnemy, Is.EqualTo((uint)hostileNpc.Id));
    }

    [Test]
    public void FindNearestPlayerEnemy_ShouldIgnoreHostileNpcs_AndReturnNearestPlayer()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x40u,
            MapId = 1,
            Location = new(100, 100, 0),
            BaseBody = 0x0003,
            Notoriety = Notoriety.CanBeAttacked
        };
        var hostileNpc = new UOMobileEntity
        {
            Id = (Serial)0x41u,
            IsPlayer = false,
            Notoriety = Notoriety.CanBeAttacked,
            MapId = 1,
            Location = new(101, 100, 0)
        };
        var playerNear = new UOMobileEntity
        {
            Id = (Serial)0x42u,
            IsPlayer = true,
            MapId = 1,
            Location = new(102, 100, 0)
        };
        spatial.AddMobile(npc);
        spatial.AddMobile(hostileNpc);
        spatial.AddMobile(playerNear);
        var module = new PerceptionModule(spatial);

        var nearestEnemy = module.FindNearestPlayerEnemy((uint)npc.Id, 20);

        Assert.That(nearestEnemy, Is.EqualTo((uint)playerNear.Id));
    }

    [Test]
    public void FindNearestPlayerEnemy_ShouldSkipSameFactionPlayer_AndReturnNearestHostilePlayer()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x50u,
            MapId = 1,
            Location = new(100, 100, 0),
            FactionId = "true_britannians"
        };
        var sameFactionPlayer = new UOMobileEntity
        {
            Id = (Serial)0x51u,
            IsPlayer = true,
            MapId = 1,
            Location = new(101, 100, 0),
            FactionId = "true_britannians"
        };
        var hostilePlayer = new UOMobileEntity
        {
            Id = (Serial)0x52u,
            IsPlayer = true,
            MapId = 1,
            Location = new(102, 100, 0),
            FactionId = "shadowlords"
        };
        spatial.AddMobile(npc);
        spatial.AddMobile(sameFactionPlayer);
        spatial.AddMobile(hostilePlayer);
        var module = new PerceptionModule(spatial, new AiRelationService(CreateFactionTemplateService()));

        var nearestEnemy = module.FindNearestPlayerEnemy((uint)npc.Id, 20);

        Assert.That(nearestEnemy, Is.EqualTo((uint)hostilePlayer.Id));
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsClosest_ShouldReturnNearestHostileCandidate()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = CreateHostileNpc((Serial)0x60u, new Point3D(100, 100, 0));
        var near = CreatePlayer((Serial)0x61u, new Point3D(101, 100, 0));
        var far = CreatePlayer((Serial)0x62u, new Point3D(106, 100, 0));
        spatial.AddMobile(npc);
        spatial.AddMobile(near);
        spatial.AddMobile(far);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "closest");

        Assert.That(target, Is.EqualTo((uint)near.Id));
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsWeakest_ShouldReturnLowestHitsCandidate()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = CreateHostileNpc((Serial)0x70u, new Point3D(100, 100, 0));
        var stronger = CreatePlayer((Serial)0x71u, new Point3D(101, 100, 0));
        stronger.Hits = 50;
        var weaker = CreatePlayer((Serial)0x72u, new Point3D(105, 100, 0));
        weaker.Hits = 10;
        spatial.AddMobile(npc);
        spatial.AddMobile(stronger);
        spatial.AddMobile(weaker);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "weakest");

        Assert.That(target, Is.EqualTo((uint)weaker.Id));
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsStrongest_ShouldReturnHighestTacticsPlusStrengthCandidate()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = CreateHostileNpc((Serial)0x80u, new Point3D(100, 100, 0));
        var weaker = CreatePlayer((Serial)0x81u, new Point3D(101, 100, 0));
        weaker.InitializeSkills();
        weaker.Strength = 40;
        weaker.SetSkill(UOSkillName.Tactics, 300);

        var stronger = CreatePlayer((Serial)0x82u, new Point3D(106, 100, 0));
        stronger.InitializeSkills();
        stronger.Strength = 90;
        stronger.SetSkill(UOSkillName.Tactics, 900);

        spatial.AddMobile(npc);
        spatial.AddMobile(weaker);
        spatial.AddMobile(stronger);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "strongest");

        Assert.That(target, Is.EqualTo((uint)stronger.Id));
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsAggressor_ShouldReturnNearestAggressionBasedTarget()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = CreateHostileNpc((Serial)0x90u, new Point3D(100, 100, 0));
        var hostileWithoutAggression = CreatePlayer((Serial)0x91u, new Point3D(101, 100, 0));
        var aggressor = CreatePlayer((Serial)0x92u, new Point3D(103, 100, 0));
        npc.RefreshAggressor(aggressor.Id, npc.Id, DateTime.UtcNow);

        spatial.AddMobile(npc);
        spatial.AddMobile(hostileWithoutAggression);
        spatial.AddMobile(aggressor);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "aggressor");

        Assert.That(target, Is.EqualTo((uint)aggressor.Id));
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsNone_ShouldReturnNull()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = CreateHostileNpc((Serial)0xA0u, new Point3D(100, 100, 0));
        var candidate = CreatePlayer((Serial)0xA1u, new Point3D(101, 100, 0));
        spatial.AddMobile(npc);
        spatial.AddMobile(candidate);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "none");

        Assert.That(target, Is.Null);
    }

    [Test]
    public void FindBestTarget_WhenFightModeIsEvil_ShouldIncludeNegativeKarmaTargets()
    {
        var spatial = new PerceptionTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0xB0u,
            MapId = 1,
            Location = new(100, 100, 0),
            Notoriety = Notoriety.Innocent
        };
        var evilPlayer = CreatePlayer((Serial)0xB1u, new Point3D(101, 100, 0));
        evilPlayer.Karma = -1500;
        var innocentPlayer = CreatePlayer((Serial)0xB2u, new Point3D(102, 100, 0));
        innocentPlayer.Karma = 1000;

        spatial.AddMobile(npc);
        spatial.AddMobile(evilPlayer);
        spatial.AddMobile(innocentPlayer);
        var module = new PerceptionModule(spatial);

        var target = module.FindBestTarget((uint)npc.Id, 20, "evil");

        Assert.That(target, Is.EqualTo((uint)evilPlayer.Id));
    }

    private static UOMobileEntity CreateHostileNpc(Serial id, Point3D location)
        => new()
        {
            Id = id,
            MapId = 1,
            Location = location,
            Notoriety = Notoriety.CanBeAttacked
        };

    private static UOMobileEntity CreatePlayer(Serial id, Point3D location)
        => new()
        {
            Id = id,
            IsPlayer = true,
            MapId = 1,
            Location = location,
            Hits = 30,
            Strength = 50
        };

    private static FactionTemplateService CreateFactionTemplateService()
    {
        var service = new FactionTemplateService();
        service.Upsert(
            new()
            {
                Id = "true_britannians",
                Name = "True Britannians",
                EnemyFactionIds = ["shadowlords"]
            }
        );
        service.Upsert(
            new()
            {
                Id = "shadowlords",
                Name = "Shadowlords",
                EnemyFactionIds = ["true_britannians"]
            }
        );

        return service;
    }
}
