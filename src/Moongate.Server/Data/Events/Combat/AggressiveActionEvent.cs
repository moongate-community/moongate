using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Combat;

/// <summary>
/// Raised when a real aggressive combat action is attempted against a defender in range.
/// </summary>
public readonly record struct AggressiveActionEvent(
    GameEventBase BaseEvent,
    Serial AttackerId,
    Serial DefenderId,
    int MapId,
    Point3D Location,
    UOMobileEntity Attacker,
    UOMobileEntity Defender
) : IGameEvent
{
    public AggressiveActionEvent(
        Serial attackerId,
        Serial defenderId,
        int mapId,
        Point3D location,
        UOMobileEntity attacker,
        UOMobileEntity defender
    )
        : this(GameEventBase.CreateNow(), attackerId, defenderId, mapId, location, attacker, defender) { }
}
