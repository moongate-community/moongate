using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one sign placement entry loaded from data/signs/signs.cfg.
/// </summary>
public readonly record struct SignEntry(
    int MapId,
    int SourceMapCode,
    Serial ItemId,
    Point3D Location,
    string Text
);
