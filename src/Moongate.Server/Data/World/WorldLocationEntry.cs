using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Data.World;

/// <summary>
/// Represents a flattened world location entry resolved from location JSON data.
/// </summary>
public readonly record struct WorldLocationEntry(
    int MapId,
    string MapName,
    string CategoryPath,
    string Name,
    Point3D Location
);
