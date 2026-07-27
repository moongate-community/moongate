using Moongate.Core.Geometry;
using Moongate.Core.Types;

namespace Moongate.Server.Data.Internal.World;

/// <summary>The pure result of evaluating one movement attempt, before any side effect is applied.</summary>
public readonly record struct MovementDecision(
    bool Accepted,
    bool PositionChanged,
    Point3D NewPosition,
    DirectionType NewDirection
);
