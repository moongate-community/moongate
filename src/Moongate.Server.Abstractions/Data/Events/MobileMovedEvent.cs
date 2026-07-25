using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record MobileMovedEvent(
    Serial Mobile,
    int FromMapId,
    Point3D FromPosition,
    int ToMapId,
    Point3D ToPosition
) : ILoopAffineEvent;
