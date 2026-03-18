using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Combat;

/// <summary>
/// Raised when the combat loop evaluates whether an attack attempt is allowed in the current region/map context.
/// </summary>
public readonly record struct CombatAttemptEvent(
    GameEventBase BaseEvent,
    Serial AttackerId,
    Serial DefenderId,
    int MapId,
    Point3D Location,
    string? RegionName,
    bool IsGuardedRegion,
    bool Allowed,
    string? BlockedReason
) : IGameEvent
{
    public CombatAttemptEvent(
        Serial attackerId,
        Serial defenderId,
        int mapId,
        Point3D location,
        string? regionName,
        bool isGuardedRegion,
        bool allowed,
        string? blockedReason
    )
        : this(
            GameEventBase.CreateNow(),
            attackerId,
            defenderId,
            mapId,
            location,
            regionName,
            isGuardedRegion,
            allowed,
            blockedReason
        ) { }
}
