using Moongate.Network.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Modules;

public sealed class GuardsModuleTests
{
    private const string GuardFocusKey = "guard_focus_serial";
    private const string ModuleTypeName = "Moongate.Server.Modules.GuardsModule, Moongate.Server";

    private sealed class GuardsModuleTestSpatialWorldService : ISpatialWorldService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];
        private readonly List<MapSector> _sectors = [];

        public void AddMobile(UOMobileEntity mobile)
        {
            _mobiles[mobile.Id] = mobile;
            var sector = new MapSector(
                mobile.MapId,
                mobile.Location.X >> MapSectorConsts.SectorShift,
                mobile.Location.Y >> MapSectorConsts.SectorShift
            );
            sector.AddEntity(mobile);
            _sectors.Add(sector);
        }

        public void AddOrUpdateItem(UOItemEntity item, int mapId)
        {
            _ = item;
            _ = mapId;
        }

        public void AddOrUpdateMobile(UOMobileEntity mobile)
            => AddMobile(mobile);

        public void AddRegion(JsonRegion region)
        {
            _ = region;
        }

        public Task<int> BroadcastToPlayersAsync(
            IGameNetworkPacket packet,
            int mapId,
            Point3D location,
            int? range = null,
            long? excludeSessionId = null
        )
        {
            _ = packet;
            _ = mapId;
            _ = location;
            _ = range;
            _ = excludeSessionId;

            return Task.FromResult(0);
        }

        public List<MapSector> GetActiveSectors()
            => [.. _sectors];

        public List<UOMobileEntity> GetMobilesInSectorRange(int mapId, int centerSectorX, int centerSectorY, int radius = 2)
        {
            _ = mapId;
            _ = centerSectorX;
            _ = centerSectorY;
            _ = radius;

            return [];
        }

        public int GetMusic(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return 0;
        }

        public List<UOItemEntity> GetNearbyItems(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<UOMobileEntity> GetNearbyMobiles(Point3D location, int range, int mapId)
        {
            _ = location;
            _ = range;
            _ = mapId;

            return [];
        }

        public List<GameSession> GetPlayersInRange(
            Point3D location,
            int range,
            int mapId,
            GameSession? excludeSession = null
        )
        {
            _ = location;
            _ = range;
            _ = mapId;
            _ = excludeSession;

            return [];
        }

        public List<UOMobileEntity> GetPlayersInSector(int mapId, int sectorX, int sectorY)
        {
            _ = mapId;
            _ = sectorX;
            _ = sectorY;

            return [];
        }

        public JsonRegion? GetRegionById(int regionId)
        {
            _ = regionId;

            return null;
        }

        public MapSector? GetSectorByLocation(int mapId, Point3D location)
        {
            _ = mapId;
            _ = location;

            return null;
        }

        public SectorSystemStats GetStats()
            => new();

        public void OnItemMoved(UOItemEntity item, int mapId, Point3D oldLocation, Point3D newLocation)
        {
            _ = item;
            _ = mapId;
            _ = oldLocation;
            _ = newLocation;
        }

        public void OnMobileMoved(UOMobileEntity mobile, Point3D oldLocation, Point3D newLocation)
        {
            _ = mobile;
            _ = oldLocation;
            _ = newLocation;
        }

        public void RemoveEntity(Serial serial)
        {
            _ = serial;
        }
    }

    private sealed class GuardsModuleTestMobileService : IMobileService
    {
        private readonly Dictionary<Serial, UOMobileEntity> _mobiles = [];

        public void Seed(UOMobileEntity mobile)
            => _mobiles[mobile.Id] = mobile;

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
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
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            throw new NotSupportedException();
        }

        public Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
        {
            _ = riderId;
            _ = mountId;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = templateId;
            _ = location;
            _ = mapId;
            _ = accountId;
            _ = cancellationToken;

            return Task.FromResult((false, (UOMobileEntity?)null));
        }
    }

    private sealed class GuardsModuleTestCombatService : ICombatService
    {
        public Task<bool> ClearCombatantAsync(Serial attackerId, CancellationToken cancellationToken = default)
        {
            _ = attackerId;
            _ = cancellationToken;

            return Task.FromResult(false);
        }

        public Task<bool> TrySetCombatantAsync(
            Serial attackerId,
            Serial defenderId,
            CancellationToken cancellationToken = default
        )
        {
            _ = attackerId;
            _ = defenderId;
            _ = cancellationToken;

            return Task.FromResult(true);
        }
    }

    private sealed class GuardsModuleTestMovementValidationService : IMovementValidationService
    {
        public bool TryResolveMove(UOMobileEntity mobile, DirectionType direction, out Point3D newLocation)
        {
            _ = direction;
            newLocation = mobile.Location;

            return true;
        }
    }

    private sealed class GuardsModuleTestPathfindingService : IPathfindingService
    {
        public bool TryFindPath(
            UOMobileEntity mobile,
            Point3D targetLocation,
            out IReadOnlyList<DirectionType> path,
            int maxVisitedNodes = 1024
        )
        {
            _ = mobile;
            _ = targetLocation;
            _ = maxVisitedNodes;
            path = [];

            return false;
        }
    }

    private sealed class GuardsModuleTestGameNetworkSessionService : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear()
        {
        }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(MoongateTCPClient client)
        {
            _ = client;

            throw new NotSupportedException();
        }

        public bool Remove(long sessionId)
        {
            _ = sessionId;

            return false;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            _ = sessionId;
            session = null!;

            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            _ = characterId;
            session = null!;

            return false;
        }
    }

    private sealed class GuardsModuleTestNpcAiRuntimeStateService : INpcAiRuntimeStateService
    {
        public void BindPromptFile(Serial npcId, string promptFile)
        {
            _ = npcId;
            _ = promptFile;
        }

        public bool TryAcquireIdle(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        {
            _ = npcId;
            _ = nowMilliseconds;
            _ = cooldownMilliseconds;

            return false;
        }

        public bool TryAcquireListener(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        {
            _ = npcId;
            _ = nowMilliseconds;
            _ = cooldownMilliseconds;

            return false;
        }

        public bool TryGetPromptFile(Serial npcId, out string? promptFile)
        {
            _ = npcId;
            promptFile = null;

            return false;
        }
    }

    [Test]
    public void SetFocus_WhenGuardAndTargetExist_ShouldPersistFocus()
    {
        var moduleType = ResolveModuleType();
        Assert.That(moduleType, Is.Not.Null, "Create Moongate.Server.Modules.GuardsModule first.");

        if (moduleType is null)
        {
            return;
        }

        var spatial = new GuardsModuleTestSpatialWorldService();
        var mobileService = new GuardsModuleTestMobileService();
        var guard = new UOMobileEntity
        {
            Id = (Serial)0x401u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x402u,
            MapId = 1,
            Location = new(106, 104, 0)
        };
        spatial.AddMobile(guard);
        spatial.AddMobile(target);
        mobileService.Seed(guard);
        mobileService.Seed(target);
        var module = CreateModule(moduleType, spatial, mobileService);

        var result = InvokeBool(moduleType, module, "SetFocus", (uint)guard.Id, (uint?)target.Id);
        var focus = InvokeNullableUInt(moduleType, module, "GetFocus", (uint)guard.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(focus, Is.EqualTo((uint?)target.Id));
                Assert.That(guard.TryGetCustomInteger(GuardFocusKey, out var focusSerial), Is.True);
                Assert.That(focusSerial, Is.EqualTo((long)target.Id));
            }
        );
    }

    [Test]
    public void TeleportToTarget_WhenTargetExists_ShouldMoveGuardToTargetLocation()
    {
        var moduleType = ResolveModuleType();
        Assert.That(moduleType, Is.Not.Null, "Create Moongate.Server.Modules.GuardsModule first.");

        if (moduleType is null)
        {
            return;
        }

        var spatial = new GuardsModuleTestSpatialWorldService();
        var mobileService = new GuardsModuleTestMobileService();
        var guard = new UOMobileEntity
        {
            Id = (Serial)0x411u,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var target = new UOMobileEntity
        {
            Id = (Serial)0x412u,
            MapId = 1,
            Location = new(111, 122, 5)
        };
        spatial.AddMobile(guard);
        spatial.AddMobile(target);
        mobileService.Seed(guard);
        mobileService.Seed(target);
        var module = CreateModule(moduleType, spatial, mobileService);

        var result = InvokeBool(moduleType, module, "TeleportToTarget", (uint)guard.Id, (uint)target.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(guard.Location, Is.EqualTo(target.Location));
                Assert.That(guard.MapId, Is.EqualTo(target.MapId));
            }
        );
    }

    private static object CreateModule(
        Type moduleType,
        GuardsModuleTestSpatialWorldService spatial,
        GuardsModuleTestMobileService mobileService
    )
    {
        var constructor = moduleType.GetConstructors().OrderByDescending(ctor => ctor.GetParameters().Length).First();
        var arguments = constructor
                        .GetParameters()
                        .Select(parameter => ResolveParameter(parameter.ParameterType, spatial, mobileService))
                        .ToArray();

        return constructor.Invoke(arguments);
    }

    private static object? ResolveParameter(
        Type parameterType,
        GuardsModuleTestSpatialWorldService spatial,
        GuardsModuleTestMobileService mobileService
    )
    {
        if (parameterType == typeof(ISpatialWorldService))
        {
            return spatial;
        }

        if (parameterType == typeof(IMobileService))
        {
            return mobileService;
        }

        if (parameterType == typeof(ICombatService))
        {
            return new GuardsModuleTestCombatService();
        }

        if (parameterType == typeof(IMovementValidationService))
        {
            return new GuardsModuleTestMovementValidationService();
        }

        if (parameterType == typeof(IPathfindingService))
        {
            return new GuardsModuleTestPathfindingService();
        }

        if (parameterType == typeof(IGameNetworkSessionService))
        {
            return new GuardsModuleTestGameNetworkSessionService();
        }

        if (parameterType == typeof(INpcAiRuntimeStateService))
        {
            return new GuardsModuleTestNpcAiRuntimeStateService();
        }

        if (!parameterType.IsValueType)
        {
            return null;
        }

        return Activator.CreateInstance(parameterType);
    }

    private static bool InvokeBool(Type moduleType, object module, string methodName, params object?[] arguments)
    {
        var method = moduleType.GetMethod(methodName);
        Assert.That(method, Is.Not.Null, $"Expected {methodName} to exist on GuardsModule.");

        var result = method!.Invoke(module, arguments);

        return result is bool boolResult && boolResult;
    }

    private static uint? InvokeNullableUInt(Type moduleType, object module, string methodName, params object?[] arguments)
    {
        var method = moduleType.GetMethod(methodName);
        Assert.That(method, Is.Not.Null, $"Expected {methodName} to exist on GuardsModule.");

        return method!.Invoke(module, arguments) is uint value ? value : null;
    }

    private static Type? ResolveModuleType()
        => Type.GetType(ModuleTypeName, throwOnError: false);
}
