using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Types.World;

public readonly record struct DoorGenerationPlacementRecord(
    int MapId,
    Point3D Location,
    DoorGenerationFacing Facing,
    int? PairGroupId = null
);
