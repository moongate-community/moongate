using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a recent aggression relationship between two mobiles.
/// </summary>
public readonly record struct AggressorInfo(
    Serial AttackerId,
    Serial DefenderId,
    DateTime LastCombatAtUtc,
    bool IsCriminal,
    bool CanReportMurder
);
