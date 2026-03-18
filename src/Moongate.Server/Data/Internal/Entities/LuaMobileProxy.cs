using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Lua-facing proxy exposing mobile primitives used by NPC brains.
/// </summary>
public sealed class LuaMobileProxy
{
    private readonly ILogger _logger = Log.ForContext<LuaMobileProxy>();
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly IMovementValidationService? _movementValidationService;
    private readonly IPathfindingService? _pathfindingService;
    private readonly IGameEventBusService? _gameEventBusService;
    private readonly IBackgroundJobService? _backgroundJobService;

    private LuaMobileProxy? _target;

    public LuaMobileProxy(
        UOMobileEntity mobile,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null
    )
    {
        Mobile = mobile;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _backgroundJobService = backgroundJobService;
    }

    public uint Serial => (uint)Mobile.Id;

    public string Name => Mobile.Name ?? string.Empty;

    internal UOMobileEntity Mobile { get; }

    public int MapId => Mobile.MapId;

    public int LocationX => Mobile.Location.X;

    public int LocationY => Mobile.Location.Y;

    public int LocationZ => Mobile.Location.Z;

    public bool IsOnline => _gameNetworkSessionService.TryGetByCharacterId(Mobile.Id, out _);

    public bool IsMountable => Mobile.IsMountable;

    public void CastSpell(int spellId)

        // TODO: Implement spell casting primitive for brain point 5.
        => _ = spellId;

    public void ClearTarget()
        => _target = null;

    public bool DisableWar()
    {
        Mobile.IsWarMode = false;

        if (_gameEventBusService is null)
        {
            return false;
        }

        _gameEventBusService.PublishAsync(new MobileWarModeChangedEvent(Mobile))
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

        return true;
    }

    public int DistanceTo(LuaMobileProxy? target)
    {
        if (target is null)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(Mobile.Location.GetDistance(new(target.LocationX, target.LocationY, target.LocationZ)));
    }

    public bool EnableWar()
    {
        Mobile.IsWarMode = true;

        if (_gameEventBusService is null)
        {
            return false;
        }

        _gameEventBusService.PublishAsync(new MobileWarModeChangedEvent(Mobile))
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

        return true;
    }

    public LuaMobileProxy? FindEnemy(int range)
    {
        if (_spatialWorldService is null || range <= 0)
        {
            return null;
        }

        var mobile = _spatialWorldService.GetNearbyMobiles(Mobile.Location, range, Mobile.MapId)
                                         .FirstOrDefault(entity => entity.Id != Mobile.Id && entity.IsPlayer);

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

        var mobile = _spatialWorldService.GetNearbyMobiles(Mobile.Location, range, Mobile.MapId)
                                         .FirstOrDefault(entity => entity.Id != Mobile.Id && !entity.IsPlayer);

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

    public void Flee(LuaMobileProxy? from)

        // TODO: Implement flee movement primitive for brain point 5.
        => _ = from;

    public double GetHpPercent()
    {
        if (Mobile.MaxHits <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)Mobile.Hits / Mobile.MaxHits, 0, 1);
    }

    public LuaMobileProxy? GetTarget()
        => _target;

    public int GetWalkingRange()
    {
        if (Mobile.TryGetCustomInteger("walking_range", out var walkingRange))
        {
            return (int)Math.Clamp(walkingRange, int.MinValue, int.MaxValue);
        }

        return 0;
    }

    public bool HasTarget()
        => _target is not null;

    public bool IsAlive()
        => Mobile.IsAlive;

    public bool IsInRange(LuaMobileProxy? target, int range)
    {
        if (target is null || range < 0)
        {
            return false;
        }

        return Mobile.Location.InRange(new(target.LocationX, target.LocationY, target.LocationZ), range);
    }

    public bool Move(DirectionType direction)
    {
        if (_movementValidationService is null)
        {
            return false;
        }

        var oldLocation = Mobile.Location;
        var oldMapId = Mobile.MapId;

        if (!_movementValidationService.TryResolveMove(Mobile, direction, out var newLocation))
        {
            return false;
        }

        Mobile.Location = newLocation;
        Mobile.Direction = direction;

        if (_gameEventBusService is not null && oldLocation != newLocation)
        {
            var sessionId = _gameNetworkSessionService.TryGetByCharacterId(Mobile.Id, out var session)
                                ? session.SessionId
                                : -1;

            _gameEventBusService.PublishAsync(
                                    new MobilePositionChangedEvent(
                                        sessionId,
                                        Mobile.Id,
                                        oldMapId,
                                        Mobile.MapId,
                                        oldLocation,
                                        newLocation,
                                        true
                                    )
                                )
                                .AsTask()
                                .GetAwaiter()
                                .GetResult();
        }

        return true;
    }

    public void MoveTowards(LuaMobileProxy? target)
    {
        if (target is null || _movementValidationService is null || _pathfindingService is null)
        {
            return;
        }

        if (target.MapId != Mobile.MapId)
        {
            return;
        }

        var targetLocation = new Point3D(target.LocationX, target.LocationY, target.LocationZ);

        if (!_pathfindingService.TryFindPath(Mobile, targetLocation, out var path) || path.Count == 0)
        {
            return;
        }

        Move(path[0]);
    }

    public void PlayAnimation(int animId)
    {
        if (_gameEventBusService is null || animId < 0)
        {
            return;
        }

        _gameEventBusService.PublishAsync(
                                new MobilePlayAnimationEvent(
                                    Mobile.Id,
                                    Mobile.MapId,
                                    Mobile.Location,
                                    AnimationUtils.ClampActionToPacket(animId)
                                )
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
    }

    public bool PlayAnimationIntent(string intentName)
    {
        if (!TryParseAnimationIntent(intentName, out var intent))
        {
            return false;
        }

        return PlayAnimationIntent(intent);
    }

    public bool PlayAnimationIntent(AnimationIntent intent)
    {
        if (_gameEventBusService is null)
        {
            return false;
        }

        if (!AnimationUtils.TryResolveAnimation(intent, Mobile.Body.Type, Mobile.IsMounted, out var animation))
        {
            return false;
        }

        _gameEventBusService.PublishAsync(
                                new MobilePlayAnimationEvent(
                                    Mobile.Id,
                                    Mobile.MapId,
                                    Mobile.Location,
                                    animation.Action,
                                    animation.FrameCount,
                                    animation.RepeatCount,
                                    animation.Forward,
                                    animation.Repeat,
                                    animation.Delay
                                )
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

        return true;
    }

    public void PlaySound(int soundId)
    {
        if (_gameEventBusService is null || soundId < 0)
        {
            return;
        }

        _gameEventBusService.PublishAsync(
                                new MobilePlaySoundEvent(
                                    Mobile.Id,
                                    Mobile.MapId,
                                    Mobile.Location,
                                    (ushort)Math.Min(soundId, ushort.MaxValue)
                                )
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
    }

    public bool Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var recipients = _speechService.SpeakAsMobileAsync(Mobile, text).GetAwaiter().GetResult();

        return recipients > 0;
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
                                    Mobile.Id,
                                    Mobile.MapId,
                                    Mobile.Location,
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
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();
    }

    public void SetTarget(LuaMobileProxy? target)
        => _target = target;

    public bool SetWarMode(bool isEnabled)
        => isEnabled ? EnableWar() : DisableWar();

    public void StopMoving() { }

    public void SummonUndead(int count)

        // TODO: Implement summon primitive for brain point 5.
        => _ = count;

    public void Swing(LuaMobileProxy? target)

        // TODO: Implement melee combat primitive for brain point 5.
        => _ = target;

    public bool Teleport(int mapId, int x, int y, int z)
    {
        if (mapId < 0)
        {
            return false;
        }

        var oldLocation = Mobile.Location;
        var oldMapId = Mobile.MapId;
        var newLocation = new Point3D(x, y, z);

        Mobile.MapId = mapId;
        Mobile.Location = newLocation;

        if (_gameEventBusService is not null && (oldMapId != Mobile.MapId || oldLocation != newLocation))
        {
            var sessionId = _gameNetworkSessionService.TryGetByCharacterId(Mobile.Id, out var session)
                                ? session.SessionId
                                : -1;

            PublishPositionChangedAsync(
                new(
                    sessionId,
                    Mobile.Id,
                    oldMapId,
                    Mobile.MapId,
                    oldLocation,
                    newLocation,
                    true
                )
            );
        }

        return true;
    }

    public void UseAnimation(int animId)
        => PlayAnimation(animId);

    public bool UseAnimation(string intentName)
        => PlayAnimationIntent(intentName);

    public bool UseAnimation(AnimationIntent intent)
        => PlayAnimationIntent(intent);

    public void WalkTo(int x, int y)
    {
        _ = x;
        _ = y;
    }

    public void Wander(int radius)

        // TODO: Implement wandering movement primitive for brain point 5.
        => _ = radius;

    private void PublishPositionChangedAsync(MobilePositionChangedEvent gameEvent)
    {
        if (_gameEventBusService is null)
        {
            return;
        }

        if (_backgroundJobService is not null)
        {
            _backgroundJobService.PostToGameLoop(() => PublishPositionChangedSynchronously(gameEvent));

            return;
        }

        var publishTask = _gameEventBusService.PublishAsync(gameEvent);

        if (publishTask.IsCompletedSuccessfully)
        {
            return;
        }

        publishTask.AsTask()
                   .ContinueWith(
                       task =>
                       {
                           if (task.Exception is null)
                           {
                               return;
                           }

                           _logger.Error(
                               task.Exception,
                               "Failed to publish teleport position change MobileId={MobileId}",
                               gameEvent.MobileId
                           );
                       },
                       TaskScheduler.Default
                   );
    }

    private void PublishPositionChangedSynchronously(MobilePositionChangedEvent gameEvent)
    {
        if (_gameEventBusService is null)
        {
            return;
        }

        try
        {
            _gameEventBusService.PublishAsync(gameEvent)
                                .AsTask()
                                .GetAwaiter()
                                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to publish teleport position change MobileId={MobileId}",
                gameEvent.MobileId
            );
        }
    }

    private static bool SetIntent(AnimationIntent value, out AnimationIntent intent)
    {
        intent = value;

        return true;
    }

    private static bool TryParseAnimationIntent(string intentName, out AnimationIntent intent)
    {
        intent = default;

        if (string.IsNullOrWhiteSpace(intentName))
        {
            return false;
        }

        var normalized = intentName.Trim().Replace("-", "_").ToLowerInvariant();

        return normalized switch
        {
            "bow"             => SetIntent(AnimationIntent.Bow, out intent),
            "salute"          => SetIntent(AnimationIntent.Salute, out intent),
            "swing"           => SetIntent(AnimationIntent.SwingPrimary, out intent),
            "swing_primary"   => SetIntent(AnimationIntent.SwingPrimary, out intent),
            "swing_secondary" => SetIntent(AnimationIntent.SwingSecondary, out intent),
            "hurt"            => SetIntent(AnimationIntent.Hurt, out intent),
            _                 => Enum.TryParse(intentName, true, out intent)
        };
    }
}
