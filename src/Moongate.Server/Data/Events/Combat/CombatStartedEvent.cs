using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Combat;

/// <summary>
/// Raised when a combatant is successfully engaged and the first swing is scheduled.
/// </summary>
public readonly record struct CombatStartedEvent(
    GameEventBase BaseEvent,
    Serial AttackerId,
    Serial DefenderId,
    int MapId,
    Point3D Location,
    UOMobileEntity Attacker
) : IGameEvent
{
    public CombatStartedEvent(
        Serial attackerId,
        Serial defenderId,
        int mapId,
        Point3D location,
        UOMobileEntity attacker
    )
        : this(GameEventBase.CreateNow(), attackerId, defenderId, mapId, location, attacker) { }
}
