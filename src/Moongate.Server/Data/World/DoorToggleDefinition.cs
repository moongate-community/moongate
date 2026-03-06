using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Data.World;

/// <summary>
/// Precomputed toggle metadata for a concrete door item id.
/// </summary>
public readonly record struct DoorToggleDefinition(
    int CurrentItemId,
    int NextItemId,
    bool IsClosed,
    Point3D Offset
);
