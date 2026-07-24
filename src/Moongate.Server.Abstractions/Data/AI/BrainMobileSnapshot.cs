using Moongate.Core.Geometry;
using Moongate.Core.Primitives;

namespace Moongate.Server.Abstractions.Data.AI;

public sealed record BrainMobileSnapshot(
    Serial Id,
    string Name,
    bool IsPlayer,
    int MapId,
    Point3D Position,
    int Hits,
    int HitsMax,
    bool Warmode,
    Serial CombatantId,
    bool Criminal,
    int Kills
);
