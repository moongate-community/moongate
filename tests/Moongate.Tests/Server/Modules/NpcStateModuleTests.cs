using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class NpcStateModuleTests
{
    private sealed class NpcStateTestSpatialWorldService : ISpatialWorldService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            _mobiles[mobile.Id] = mobile;
            var sectorX = mobile.Location.X >> MapSectorConsts.SectorShift;
            var sectorY = mobile.Location.Y >> MapSectorConsts.SectorShift;
            var sector = new MapSector(mobile.MapId, sectorX, sectorY);
            sector.AddEntity(mobile);
            _sectors.Add(sector);
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
            => [.. _sectors];

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

        public void RemoveEntity(Serial serial) { }
    }

    private static (NpcStateModule Module, UOMobileEntity Npc) CreateModuleWithNpc(uint npcSerial)
    {
        var spatial = new NpcStateTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)npcSerial,
            MapId = 1,
            Location = new(100, 100, 0)
        };

        spatial.AddMobile(npc);

        return (new NpcStateModule(spatial), npc);
    }

    [Test]
    public void GetHpPercent_AndIsAlive_ShouldReadNpcState()
    {
        var spatial = new NpcStateTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x100u,
            MapId = 1,
            Location = new(100, 100, 0),
            IsAlive = true,
            Hits = 30,
            MaxHits = 60
        };
        spatial.AddMobile(npc);
        var module = new NpcStateModule(spatial);

        var hpPercent = module.GetHpPercent((uint)npc.Id);
        var isAlive = module.IsAlive((uint)npc.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(hpPercent, Is.EqualTo(0.5).Within(0.0001));
                Assert.That(isAlive, Is.True);
            }
        );
    }

    [Test]
    public void SetVar_AndGetVar_ShouldHandleTypedValues_AndNilRemoval()
    {
        var spatial = new NpcStateTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x101u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatial.AddMobile(npc);
        var module = new NpcStateModule(spatial);

        var setBool = module.SetVar((uint)npc.Id, "is_alert", true);
        var setNumber = module.SetVar((uint)npc.Id, "threat_level", 42);
        var setText = module.SetVar((uint)npc.Id, "mode", "guard");
        var isAlert = module.GetVar((uint)npc.Id, "is_alert");
        var threatLevel = module.GetVar((uint)npc.Id, "threat_level");
        var mode = module.GetVar((uint)npc.Id, "mode");
        var removed = module.SetVar((uint)npc.Id, "mode", null);
        var missing = module.GetVar((uint)npc.Id, "mode");

        Assert.Multiple(
            () =>
            {
                Assert.That(setBool, Is.True);
                Assert.That(setNumber, Is.True);
                Assert.That(setText, Is.True);
                Assert.That(isAlert, Is.EqualTo(true));
                Assert.That(threatLevel, Is.EqualTo(42L));
                Assert.That(mode, Is.EqualTo("guard"));
                Assert.That(removed, Is.True);
                Assert.That(missing, Is.Null);
            }
        );
    }

    [Test]
    public void SetVar_CanonicalAiKeys_ShouldWriteAndClearLegacyAliases()
    {
        var (module, npc) = CreateModuleWithNpc(0x102u);
        npc.SetCustomString("modernuo_action", "wander");
        npc.SetCustomInteger("modernuo_target_serial", 0x222u);

        var setAction = module.SetVar((uint)npc.Id, "ai_action", "combat");
        var setTarget = module.SetVar((uint)npc.Id, "ai_target_serial", 0x333u);
        var action = module.GetVar((uint)npc.Id, "ai_action");
        var target = module.GetVar((uint)npc.Id, "ai_target_serial");

        Assert.Multiple(
            () =>
            {
                Assert.That(setAction, Is.True);
                Assert.That(setTarget, Is.True);
                Assert.That(action, Is.EqualTo("combat"));
                Assert.That(target, Is.EqualTo(0x333L));
                Assert.That(npc.CustomProperties.ContainsKey("ai_action"), Is.True);
                Assert.That(npc.CustomProperties.ContainsKey("ai_target_serial"), Is.True);
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_action"), Is.False);
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_target_serial"), Is.False);
            }
        );
    }

    [Test]
    public void GetVar_LegacyAiActionKey_ShouldMigrateToCanonicalKey()
    {
        var (module, npc) = CreateModuleWithNpc(0x103u);
        npc.SetCustomString("modernuo_action", "guard");

        var action = module.GetVar((uint)npc.Id, "ai_action");

        Assert.Multiple(
            () =>
            {
                Assert.That(action, Is.EqualTo("guard"));
                Assert.That(npc.CustomProperties.ContainsKey("ai_action"), Is.True);
                Assert.That(npc.CustomProperties["ai_action"].Type, Is.EqualTo(ItemCustomPropertyType.String));
                Assert.That(npc.CustomProperties["ai_action"].StringValue, Is.EqualTo("guard"));
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_action"), Is.False);
            }
        );
    }

    [Test]
    public void GetVar_LegacyAiTargetSerialKey_ShouldMigrateToCanonicalKey()
    {
        var (module, npc) = CreateModuleWithNpc(0x104u);
        npc.SetCustomInteger("modernuo_target_serial", 0x444u);

        var target = module.GetVar((uint)npc.Id, "ai_target_serial");

        Assert.Multiple(
            () =>
            {
                Assert.That(target, Is.EqualTo(0x444L));
                Assert.That(npc.CustomProperties.ContainsKey("ai_target_serial"), Is.True);
                Assert.That(npc.CustomProperties["ai_target_serial"].Type, Is.EqualTo(ItemCustomPropertyType.Integer));
                Assert.That(npc.CustomProperties["ai_target_serial"].IntegerValue, Is.EqualTo(0x444L));
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_target_serial"), Is.False);
            }
        );
    }

    [Test]
    public void SetVar_CanonicalAiKeysWithNil_ShouldClearCanonicalAndLegacyAliases()
    {
        var (module, npc) = CreateModuleWithNpc(0x105u);
        npc.SetCustomString("ai_action", "combat");
        npc.SetCustomString("modernuo_action", "guard");
        npc.SetCustomInteger("ai_target_serial", 0x555u);
        npc.SetCustomInteger("modernuo_target_serial", 0x666u);

        var removeAction = module.SetVar((uint)npc.Id, "ai_action", null);
        var removeTarget = module.SetVar((uint)npc.Id, "ai_target_serial", null);

        Assert.Multiple(
            () =>
            {
                Assert.That(removeAction, Is.True);
                Assert.That(removeTarget, Is.True);
                Assert.That(npc.CustomProperties.ContainsKey("ai_action"), Is.False);
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_action"), Is.False);
                Assert.That(npc.CustomProperties.ContainsKey("ai_target_serial"), Is.False);
                Assert.That(npc.CustomProperties.ContainsKey("modernuo_target_serial"), Is.False);
            }
        );
    }
}
