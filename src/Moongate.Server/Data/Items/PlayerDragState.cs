using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Items;

/// <summary>
/// Represents a pending item drag state for a player session.
/// </summary>
public readonly record struct PlayerDragState(
    Serial ItemId,
    int Amount,
    Serial SourceContainerId,
    Point3D SourceLocation
);
