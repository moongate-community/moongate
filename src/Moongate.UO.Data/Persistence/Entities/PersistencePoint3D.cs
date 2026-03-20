using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Geometry;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistencePoint3D
{
    [MoongatePersistedMember(0)]
    public int X { get; set; }

    [MoongatePersistedMember(1)]
    public int Y { get; set; }

    [MoongatePersistedMember(2)]
    public int Z { get; set; }

    public Point3D ToPoint3D() => new(X, Y, Z);

    public static PersistencePoint3D FromPoint3D(Point3D point)
        => new()
        {
            X = point.X,
            Y = point.Y,
            Z = point.Z
        };
}
