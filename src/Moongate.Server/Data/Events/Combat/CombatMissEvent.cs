using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Combat;

/// <summary>
/// Raised when a scheduled melee swing resolves as a miss.
/// </summary>
public readonly record struct CombatMissEvent(
    GameEventBase BaseEvent,
    Serial AttackerId,
    Serial DefenderId,
    int MapId,
    Point3D Location
) : IGameEvent
{
    public CombatMissEvent(
        Serial attackerId,
        Serial defenderId,
        int mapId,
        Point3D location
    )
        : this(GameEventBase.CreateNow(), attackerId, defenderId, mapId, location) { }
}
