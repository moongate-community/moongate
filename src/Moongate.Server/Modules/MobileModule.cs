using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("mobile", "Provides helpers to resolve mobiles from scripts.")]

/// <summary>
/// Exposes mobile lookup helpers to Lua scripts.
/// </summary>
public sealed class MobileModule
{
    private static bool _isLuaMobileProxyTypeRegistered;
    private readonly ICharacterService _characterService;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;
    private readonly IBackgroundJobService? _backgroundJobService;
    private readonly IMobileService? _mobileService;
    private readonly IOutgoingPacketQueue? _outgoingPacketQueue;

    public MobileModule(
        ICharacterService characterService,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null,
        IMobileService? mobileService = null,
        IOutgoingPacketQueue? outgoingPacketQueue = null
    )
    {
        _characterService = characterService;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _backgroundJobService = backgroundJobService;
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    [ScriptFunction("get", "Gets a mobile reference by character id, or nil when not found.")]
    public LuaMobileProxy? Get(uint characterId)
    {
        if (characterId == 0)
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var mobileId = (Serial)characterId;
        var mobile = TryResolveRuntimeMobile(mobileId) ??
                     _characterService.GetCharacterAsync(mobileId).GetAwaiter().GetResult();

        return mobile is null
                   ? null
                   : new(
                       mobile,
                       _speechService,
                       _gameNetworkSessionService,
                       _spatialWorldService,
                       _movementValidationService,
                       _pathfindingService,
                       _gameEventBusService,
                       _backgroundJobService
                   );
    }

    [ScriptFunction("try_mount", "Attempts to mount the rider on the target mount creature.")]
    public bool TryMount(uint riderId, uint mountId)
    {
        if (riderId == 0 || mountId == 0 || _mobileService is null)
        {
            return false;
        }

        var riderSerial = (Serial)riderId;
        var mountSerial = (Serial)mountId;
        var rider = ResolveRiderForMount(riderSerial);
        var mount = TryResolveRuntimeMobile(mountSerial) ??
                    _mobileService.GetAsync(mountSerial).GetAwaiter().GetResult();

        if (rider is not null)
        {
            _mobileService.CreateOrUpdateAsync(rider).GetAwaiter().GetResult();
        }

        if (mount is not null)
        {
            _mobileService.CreateOrUpdateAsync(mount).GetAwaiter().GetResult();
        }

        var mounted = _mobileService.TryMountAsync(riderSerial, mountSerial).GetAwaiter().GetResult();

        if (mounted)
        {
            RefreshMountedSession(riderSerial, mountSerial, true);
        }

        return mounted;
    }

    [ScriptFunction("dismount", "Attempts to dismount the rider from the current mount.")]
    public bool Dismount(uint riderId)
    {
        if (riderId == 0 || _mobileService is null)
        {
            return false;
        }

        var dismounted = _mobileService.DismountAsync((Serial)riderId).GetAwaiter().GetResult();

        if (dismounted)
        {
            RefreshMountedSession((Serial)riderId, Serial.Zero, false);
        }

        return dismounted;
    }

    [ScriptFunction("spawn", "Spawns a mobile template at world position { x, y, z, map_id }.")]
    public LuaMobileProxy? Spawn(string mobileTemplateId, Table? position)
    {
        if (_mobileService is null ||
            string.IsNullOrWhiteSpace(mobileTemplateId) ||
            !TryParsePosition(position, out var location, out var mapId))
        {
            return null;
        }

        RegisterLuaTypeIfNeeded();
        var mobile = _mobileService.SpawnFromTemplateAsync(mobileTemplateId.Trim(), location, mapId)
                                   .GetAwaiter()
                                   .GetResult();

        return new(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService,
            _backgroundJobService
        );
    }

    private static void RegisterLuaTypeIfNeeded()
    {
        if (_isLuaMobileProxyTypeRegistered)
        {
            return;
        }

        var type = typeof(LuaMobileProxy);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
        _isLuaMobileProxyTypeRegistered = true;
    }

    private UOMobileEntity? TryResolveRuntimeMobile(Serial mobileId)
    {
        if (_spatialWorldService is null)
        {
            return null;
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var runtimeMobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (runtimeMobile is not null)
            {
                return runtimeMobile;
            }
        }

        return null;
    }

    private UOMobileEntity? ResolveRiderForMount(Serial riderId)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(riderId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return TryResolveRuntimeMobile(riderId) ??
               _mobileService?.GetAsync(riderId).GetAwaiter().GetResult();
    }

    private void RefreshMountedSession(Serial riderId, Serial mountId, bool isMounted)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(riderId, out var session) || session.Character is null)
        {
            return;
        }

        var rider = TryResolveRuntimeMobile(riderId) ??
                    _mobileService?.GetAsync(riderId).GetAwaiter().GetResult() ??
                    session.Character;
        var mount = mountId == Serial.Zero
                        ? null
                        : TryResolveRuntimeMobile(mountId) ??
                          _mobileService?.GetAsync(mountId).GetAwaiter().GetResult();

        if (_outgoingPacketQueue is null)
        {
            return;
        }

        MountedSelfRefreshHelper.Refresh(session, _outgoingPacketQueue, rider, mount, isMounted);
    }

    private static bool TryGetRequiredInt(Table table, string key, out int value)
    {
        value = 0;
        var dyn = table.Get(key);

        switch (dyn.Type)
        {
            case DataType.Number:
                value = (int)dyn.Number;

                return true;
            case DataType.String when int.TryParse(dyn.String, out var parsed):
                value = parsed;

                return true;
            default:
                return false;
        }
    }

    private static bool TryParsePosition(Table? position, out Point3D location, out int mapId)
    {
        location = Point3D.Zero;
        mapId = 0;

        if (position is null)
        {
            return false;
        }

        if (!TryGetRequiredInt(position, "x", out var x) ||
            !TryGetRequiredInt(position, "y", out var y) ||
            !TryGetRequiredInt(position, "z", out var z) ||
            !TryGetRequiredInt(position, "map_id", out mapId))
        {
            return false;
        }

        location = new(x, y, z);

        return true;
    }
}
