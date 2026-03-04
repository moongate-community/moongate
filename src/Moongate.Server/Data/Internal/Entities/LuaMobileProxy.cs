using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Lua-facing proxy exposing mobile primitives used by NPC brains.
/// </summary>
public sealed class LuaMobileProxy
{
    private readonly UOMobileEntity _mobile;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;

    private LuaMobileProxy? _target;

    public LuaMobileProxy(
        UOMobileEntity mobile,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null
    )
    {
        _mobile = mobile;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
    }

    public uint Serial => (uint)_mobile.Id;

    public string Name => _mobile.Name ?? string.Empty;

    public int MapId => _mobile.MapId;

    public int LocationX => _mobile.Location.X;

    public int LocationY => _mobile.Location.Y;

    public int LocationZ => _mobile.Location.Z;

    public bool IsOnline => _gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out _);

    public bool IsAlive()
        => _mobile.IsAlive;

    public double GetHpPercent()
    {
        if (_mobile.MaxHits <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)_mobile.Hits / _mobile.MaxHits, 0, 1);
    }

    public bool HasTarget()
        => _target is not null;

    public LuaMobileProxy? GetTarget()
        => _target;

    public LuaMobileProxy? FindEnemy(int range)
    {
        if (_spatialWorldService is null || range <= 0)
        {
            return null;
        }

        var mobile = _spatialWorldService.GetNearbyMobiles(_mobile.Location, range, _mobile.MapId)
                                         .FirstOrDefault(entity => entity.Id != _mobile.Id && entity.IsPlayer);

        if (mobile is null)
        {
            return null;
        }

        return new(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService
        );
    }

    public LuaMobileProxy? FindFriend(int range)
    {
        if (_spatialWorldService is null || range <= 0)
        {
            return null;
        }

        var mobile = _spatialWorldService.GetNearbyMobiles(_mobile.Location, range, _mobile.MapId)
                                         .FirstOrDefault(entity => entity.Id != _mobile.Id && !entity.IsPlayer);

        if (mobile is null)
        {
            return null;
        }

        return new(
            mobile,
            _speechService,
            _gameNetworkSessionService,
            _spatialWorldService,
            _movementValidationService,
            _pathfindingService,
            _gameEventBusService
        );
    }

    public bool IsInRange(LuaMobileProxy? target, int range)
    {
        if (target is null || range < 0)
        {
            return false;
        }

        return _mobile.Location.InRange(new Point3D(target.LocationX, target.LocationY, target.LocationZ), range);
    }

    public int DistanceTo(LuaMobileProxy? target)
    {
        if (target is null)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(
            _mobile.Location.GetDistance(new Point3D(target.LocationX, target.LocationY, target.LocationZ))
        );
    }

    public void MoveTowards(LuaMobileProxy? target)
    {
        if (target is null || _movementValidationService is null || _pathfindingService is null)
        {
            return;
        }

        if (target.MapId != _mobile.MapId)
        {
            return;
        }

        var targetLocation = new Point3D(target.LocationX, target.LocationY, target.LocationZ);

        if (!_pathfindingService.TryFindPath(_mobile, targetLocation, out var path) || path.Count == 0)
        {
            return;
        }

        Move(path[0]);
    }

    public bool Move(DirectionType direction)
    {
        if (_movementValidationService is null)
        {
            return false;
        }

        var oldLocation = _mobile.Location;

        if (!_movementValidationService.TryResolveMove(_mobile, direction, out var newLocation))
        {
            return false;
        }

        _mobile.Location = newLocation;
        _mobile.Direction = direction;

        if (_gameEventBusService is not null && oldLocation != newLocation)
        {
            var sessionId = _gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out var session)
                                ? session.SessionId
                                : -1;
            var oldMapId = _mobile.MapId;

            _gameEventBusService.PublishAsync(
                new MobilePositionChangedEvent(
                    sessionId,
                    _mobile.Id,
                    oldMapId,
                    _mobile.MapId,
                    oldLocation,
                    newLocation
                )
            ).AsTask().GetAwaiter().GetResult();
        }

        return true;
    }

    public bool Teleport(int mapId, int x, int y, int z)
    {
        if (mapId < 0)
        {
            return false;
        }

        var oldLocation = _mobile.Location;
        var oldMapId = _mobile.MapId;
        var newLocation = new Point3D(x, y, z);

        _mobile.MapId = mapId;
        _mobile.Location = newLocation;

        if (_gameEventBusService is not null && oldLocation != newLocation)
        {
            var sessionId = _gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out var session)
                                ? session.SessionId
                                : -1;

            _gameEventBusService.PublishAsync(
                new MobilePositionChangedEvent(
                    sessionId,
                    _mobile.Id,
                    oldMapId,
                    _mobile.MapId,
                    oldLocation,
                    newLocation
                )
            ).AsTask().GetAwaiter().GetResult();
        }

        return true;
    }

    public void Wander(int radius)

        // TODO: Implement wandering movement primitive for brain point 5.
        => _ = radius;

    public void Flee(LuaMobileProxy? from)

        // TODO: Implement flee movement primitive for brain point 5.
        => _ = from;

    public void WalkTo(int x, int y)
    {
        _ = x;
        _ = y;
    }

    public void StopMoving() { }

    public void Swing(LuaMobileProxy? target)

        // TODO: Implement melee combat primitive for brain point 5.
        => _ = target;

    public void SetTarget(LuaMobileProxy? target)
        => _target = target;

    public void ClearTarget()
        => _target = null;

    public void CastSpell(int spellId)

        // TODO: Implement spell casting primitive for brain point 5.
        => _ = spellId;

    public bool Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var recipients = _speechService.SpeakAsMobileAsync(_mobile, text).GetAwaiter().GetResult();

        return recipients > 0;
    }

    public void PlaySound(int soundId)
    {
        if (_gameEventBusService is null || soundId < 0)
        {
            return;
        }

        _gameEventBusService.PublishAsync(
            new MobilePlaySoundEvent(
                _mobile.Id,
                _mobile.MapId,
                _mobile.Location,
                (ushort)Math.Min(soundId, ushort.MaxValue),
                0x01,
                0
            )
        ).AsTask().GetAwaiter().GetResult();
    }

    public void SetEffect(
        int itemId,
        int speed = 10,
        int duration = 10,
        int hue = 0,
        int renderMode = 0,
        int effect = 0,
        int explodeEffect = 0,
        int explodeSound = 0,
        int layer = 0xFF,
        int unknown3 = 0
    )
    {
        if (_gameEventBusService is null || itemId < 0)
        {
            return;
        }

        _gameEventBusService.PublishAsync(
            new MobilePlayEffectEvent(
                _mobile.Id,
                _mobile.MapId,
                _mobile.Location,
                (ushort)Math.Min(itemId, ushort.MaxValue),
                (byte)Math.Clamp(speed, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(duration, byte.MinValue, byte.MaxValue),
                hue,
                renderMode,
                (ushort)Math.Clamp(effect, ushort.MinValue, ushort.MaxValue),
                (ushort)Math.Clamp(explodeEffect, ushort.MinValue, ushort.MaxValue),
                (ushort)Math.Clamp(explodeSound, ushort.MinValue, ushort.MaxValue),
                (byte)Math.Clamp(layer, byte.MinValue, byte.MaxValue),
                (ushort)Math.Clamp(unknown3, ushort.MinValue, ushort.MaxValue)
            )
        ).AsTask().GetAwaiter().GetResult();
    }

    public void PlayAnimation(int animId)

        // TODO: Implement animation primitive for brain point 5.
        => _ = animId;

    public void SummonUndead(int count)

        // TODO: Implement summon primitive for brain point 5.
        => _ = count;
}
