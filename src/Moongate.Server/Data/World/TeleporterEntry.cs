using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one world teleporter mapping loaded from data/teleporters/teleporters.json.
/// </summary>
public readonly record struct TeleporterEntry(
    int SourceMapId,
    string SourceMapName,
    Point3D SourceLocation,
    int DestinationMapId,
    string DestinationMapName,
    Point3D DestinationLocation,
    bool Back
);
