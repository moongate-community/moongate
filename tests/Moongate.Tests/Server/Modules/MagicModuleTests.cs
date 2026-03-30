using System.Reflection;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class MagicModuleTests
{
    private sealed class RecordingMagicService : IMagicService
    {
        public bool IsCastingResult { get; set; }

        public Serial? LastCasterId { get; private set; }

        public int IsCastingCalls { get; private set; }

        public int InterruptCalls { get; private set; }

        public bool IsCasting(Serial casterId)
        {
            LastCasterId = casterId;
            IsCastingCalls++;

            return IsCastingResult;
        }

        public bool TrySetTarget(Serial casterId, int spellId, Serial targetId)
        {
            _ = casterId;
            _ = spellId;
            _ = targetId;

            return false;
        }

        public ValueTask<bool> TrySetTargetAsync(
            Serial casterId,
            int spellId,
            SpellTargetData target,
            CancellationToken cancellationToken = default
        )
        {
            _ = target;
            _ = cancellationToken;

            return ValueTask.FromResult(TrySetTarget(casterId, spellId, target.TargetId));
        }

        public ValueTask<bool> TryCastAsync(
            UOMobileEntity caster,
            int spellId,
            CancellationToken cancellationToken = default
        )
        {
            _ = caster;
            _ = spellId;
            _ = cancellationToken;

            return ValueTask.FromResult(false);
        }

        public void Interrupt(Serial casterId)
        {
            LastCasterId = casterId;
            InterruptCalls++;
        }

        public ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default)
        {
            _ = casterId;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class MagicModuleTestSpatialWorldService : ISpatialWorldService
    {
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            var sector = new MapSector(
                mobile.MapId,
                mobile.Location.X >> MapSectorConsts.SectorShift,
                mobile.Location.Y >> MapSectorConsts.SectorShift
            );
            sector.AddEntity(mobile);
            _sectors.Add(sector);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId) { }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

        public void AddRegion(JsonRegion region) { }

        public Task<int> BroadcastToPlayersAsync(
            Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet,
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

        public List<Moongate.Server.Data.Session.GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            Moongate.Server.Data.Session.GameSession? excludeSession = null
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
    public void ScriptModule_ShouldExposeExpectedLuaModuleAndFunctions()
    {
        var moduleType = typeof(MagicModule);

        var moduleAttribute = moduleType.GetCustomAttributes(typeof(ScriptModuleAttribute), inherit: false)
                                        .Cast<ScriptModuleAttribute>()
                                        .SingleOrDefault();

        Assert.That(moduleAttribute, Is.Not.Null, "Expected MagicModule to declare ScriptModuleAttribute.");
        Assert.That(moduleAttribute!.Name, Is.EqualTo("magic"));

        Assert.Multiple(
            () =>
            {
                Assert.That(GetScriptFunctionName(moduleType, "IsCasting"), Is.EqualTo("is_casting"));
                Assert.That(GetScriptFunctionName(moduleType, "Interrupt"), Is.EqualTo("interrupt"));
            }
        );
    }

    [Test]
    public void IsCasting_AndInterrupt_ShouldDelegateToMagicService()
    {
        var spatial = new MagicModuleTestSpatialWorldService();
        var npc = new UOMobileEntity
        {
            Id = (Serial)0x501u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        spatial.AddMobile(npc);
        var magicService = new RecordingMagicService
        {
            IsCastingResult = true
        };
        var module = new MagicModule(spatial, magicService);

        var isCasting = module.IsCasting((uint)npc.Id);
        _ = module.Interrupt((uint)npc.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(isCasting, Is.True);
                Assert.That(magicService.IsCastingCalls, Is.EqualTo(1));
                Assert.That(magicService.InterruptCalls, Is.EqualTo(1));
                Assert.That(magicService.LastCasterId, Is.EqualTo(npc.Id));
            }
        );
    }

    private static string GetScriptFunctionName(Type moduleType, string methodName)
    {
        var method = moduleType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"Expected method {methodName} to exist.");

        if (method is null)
        {
            return string.Empty;
        }

        var attribute = method.GetCustomAttributes(typeof(ScriptFunctionAttribute), inherit: false)
                              .Cast<ScriptFunctionAttribute>()
                              .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null, $"Expected {methodName} to declare ScriptFunctionAttribute.");

        return attribute?.FunctionName ?? string.Empty;
    }
}
