using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Types.World;

public readonly record struct DoorGenerationMapSpec(
    int MapId,
    IReadOnlyList<Rectangle2D> Regions
);
