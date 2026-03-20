using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Geometry;

namespace Moongate.UO.Data.Persistence.Entities;

[MoongatePersistedEntity]
public sealed class PersistencePoint2D
{
    [MoongatePersistedMember(0)]
    public int X { get; set; }

    [MoongatePersistedMember(1)]
    public int Y { get; set; }

    public Point2D ToPoint2D() => new(X, Y);

    public static PersistencePoint2D FromPoint2D(Point2D point)
        => new()
        {
            X = point.X,
            Y = point.Y
        };
}
