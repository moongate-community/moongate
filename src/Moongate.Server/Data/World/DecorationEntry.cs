using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one decoration placement entry loaded from ModernUO-style .cfg files.
/// </summary>
public readonly record struct DecorationEntry(
    int MapId,
    string SourceGroup,
    string SourceFile,
    string TypeName,
    Serial ItemId,
    IReadOnlyList<string> Parameters,
    Point3D Location,
    string Extra
);
