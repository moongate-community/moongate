using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Events.Combat;

/// <summary>
/// Raised when a scheduled melee swing hits and applies damage.
/// </summary>
public readonly record struct CombatHitEvent(
    GameEventBase BaseEvent,
    Serial AttackerId,
    Serial DefenderId,
    int MapId,
    Point3D Location,
    int Damage,
    UOMobileEntity Defender
) : IGameEvent
{
    public CombatHitEvent(
        Serial attackerId,
        Serial defenderId,
        int mapId,
        Point3D location,
        int damage,
        UOMobileEntity defender
    )
        : this(GameEventBase.CreateNow(), attackerId, defenderId, mapId, location, damage, defender) { }
}
