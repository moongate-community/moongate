using Moongate.Core.Geometry;

namespace Moongate.Server.Abstractions.Data.AI;

public sealed record BrainContext(
    DateTimeOffset Now,
    BrainMobileSnapshot Self,
    int HomeMapId,
    Point3D HomePosition,
    IReadOnlyList<BrainMobileSnapshot> Nearby
);
