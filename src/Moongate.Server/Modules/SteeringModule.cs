using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

[ScriptModule("steering", "Provides movement steering helpers for NPC brains.")]

/// <summary>
/// Exposes follow, evade and wander movement primitives to Lua brains.
/// </summary>
public sealed class SteeringModule
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IMovementValidationService _movementValidationService;
    private readonly IPathfindingService _pathfindingService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public SteeringModule(
        ISpatialWorldService spatialWorldService,
        IMovementValidationService movementValidationService,
        IPathfindingService pathfindingService,
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _spatialWorldService = spatialWorldService;
        _movementValidationService = movementValidationService;
        _pathfindingService = pathfindingService;
        _gameEventBusService = gameEventBusService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    [ScriptFunction("evade", "Moves npc away from a threat until desired_range is reached.")]
    public bool Evade(uint npcSerial, uint threatSerial, int desiredRange, int repathMs = 250)
    {
        _ = repathMs;

        if (desiredRange <= 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, threatSerial, out var threat))
        {
            return false;
        }

        if (npc!.MapId != threat!.MapId)
        {
            return false;
        }

        if (!npc.Location.InRange(threat.Location, desiredRange))
        {
            return false;
        }

        var directionAway = threat.Location.GetDirectionTo(npc.Location);

        if (TryMove(npc, directionAway))
        {
            return true;
        }

        var fallbackDirections = new[]
        {
            Rotate(directionAway, -1),
            Rotate(directionAway, 1),
            Rotate(directionAway, -2),
            Rotate(directionAway, 2)
        };

        foreach (var fallbackDirection in fallbackDirections)
        {
            if (TryMove(npc, fallbackDirection))
            {
                return true;
            }
        }

        return false;
    }

    [ScriptFunction("follow", "Moves npc toward target until stop_range is reached.")]
    public bool Follow(uint npcSerial, uint targetSerial, int stopRange, int repathMs = 250)
    {
        _ = repathMs;

        if (stopRange < 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var target))
        {
            return false;
        }

        if (npc!.MapId != target!.MapId)
        {
            return false;
        }

        if (npc.Location.InRange(target.Location, stopRange))
        {
            return false;
        }

        if (_pathfindingService.TryFindPath(npc, target.Location, out var path) && path.Count > 0)
        {
            return TryMove(npc, path[0]);
        }

        return TryMove(npc, npc.Location.GetDirectionTo(target.Location));
    }

    [ScriptFunction("move_to", "Moves npc toward a world point until stop_range is reached.")]
    public bool MoveTo(uint npcSerial, int x, int y, int z, int stopRange, int repathMs = 250)
    {
        _ = repathMs;

        if (stopRange < 0 || !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        var targetLocation = new Point3D(x, y, z);

        if (npc!.Location.InRange(targetLocation, stopRange))
        {
            return false;
        }

        if (_pathfindingService.TryFindPath(npc, targetLocation, out var path) && path.Count > 0)
        {
            return TryMove(npc, path[0]);
        }

        return TryMove(npc, npc.Location.GetDirectionTo(targetLocation));
    }

    [ScriptFunction("stop", "No-op steering stop primitive placeholder.")]
    public bool Stop(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out _))
        {
            return false;
        }

        return true;
    }

    [ScriptFunction("wander", "Moves npc by one random base direction within current movement validation rules.")]
    public bool Wander(uint npcSerial, int radius = 4)
    {
        _ = radius;

        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        var candidates = new[]
        {
            DirectionType.North,
            DirectionType.NorthEast,
            DirectionType.East,
            DirectionType.SouthEast,
            DirectionType.South,
            DirectionType.SouthWest,
            DirectionType.West,
            DirectionType.NorthWest
        };
        var start = Random.Shared.Next(candidates.Length);

        for (var i = 0; i < candidates.Length; i++)
        {
            var direction = candidates[(start + i) % candidates.Length];

            if (TryMove(npc!, direction))
            {
                return true;
            }
        }

        return false;
    }

    private static DirectionType Rotate(DirectionType direction, int step)
    {
        var baseDirection = Point3D.GetBaseDirection(direction);
        var value = ((int)baseDirection + step) % 8;

        if (value < 0)
        {
            value += 8;
        }

        return (DirectionType)value;
    }

    private bool TryMove(UOMobileEntity mobile, DirectionType direction)
    {
        var baseDirection = Point3D.GetBaseDirection(direction);
        var oldLocation = mobile.Location;
        var oldMapId = mobile.MapId;

        if (!_movementValidationService.TryResolveMove(mobile, baseDirection, out var newLocation))
        {
            return false;
        }

        if (oldLocation == newLocation)
        {
            return false;
        }

        mobile.Location = newLocation;
        mobile.Direction = baseDirection;

        var sessionId = _gameNetworkSessionService.TryGetByCharacterId(mobile.Id, out var session)
                            ? session.SessionId
                            : -1;

        _gameEventBusService.PublishAsync(
                                new MobilePositionChangedEvent(
                                    sessionId,
                                    mobile.Id,
                                    oldMapId,
                                    mobile.MapId,
                                    oldLocation,
                                    newLocation
                                )
                            )
                            .AsTask()
                            .GetAwaiter()
                            .GetResult();

        return true;
    }
}
