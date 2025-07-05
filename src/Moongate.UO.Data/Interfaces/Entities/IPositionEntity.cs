using Moongate.UO.Data.Geometry;

namespace Moongate.UO.Data.Interfaces.Entities;

public interface IPositionEntity
{
    Point3D Location { get; }

    void MoveTo(Point3D newLocation);
}
