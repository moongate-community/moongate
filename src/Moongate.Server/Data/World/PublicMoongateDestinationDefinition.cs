using Moongate.UO.Data.Geometry;

namespace Moongate.Server.Data.World;

/// <summary>
/// Describes a single destination in the shared public moongate network.
/// </summary>
public sealed record PublicMoongateDestinationDefinition(
    string Id,
    string Name,
    int MapId,
    Point3D Location
);
