using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Items;

/// <summary>
/// Result payload for a successful item drop-to-ground operation.
/// </summary>
public readonly record struct DropItemToGroundResult(
    Serial ItemId,
    Serial SourceContainerId,
    Point3D OldLocation,
    Point3D NewLocation
);
