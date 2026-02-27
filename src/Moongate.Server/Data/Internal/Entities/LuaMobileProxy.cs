using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;

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

    private LuaMobileProxy? _target;

    public LuaMobileProxy(
        UOMobileEntity mobile,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null
    )
    {
        _mobile = mobile;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
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

        return new(mobile, _speechService, _gameNetworkSessionService, _spatialWorldService);
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

        return new(mobile, _speechService, _gameNetworkSessionService, _spatialWorldService);
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

        return (int)Math.Round(_mobile.Location.GetDistance(new Point3D(target.LocationX, target.LocationY, target.LocationZ)));
    }

    public void MoveTowards(LuaMobileProxy? target)
        // TODO: Implement navigation/pathfinding primitive for brain point 5.
        => _ = target;

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

        if (!_gameNetworkSessionService.TryGetByCharacterId(_mobile.Id, out var session))
        {
            return false;
        }

        return _speechService.SendMessageFromServerAsync(session, text).GetAwaiter().GetResult();
    }

    public void PlaySound(int soundId)
        // TODO: Implement sound effect primitive for brain point 5.
        => _ = soundId;

    public void PlayAnimation(int animId)
        // TODO: Implement animation primitive for brain point 5.
        => _ = animId;

    public void SummonUndead(int count)
        // TODO: Implement summon primitive for brain point 5.
        => _ = count;
}
