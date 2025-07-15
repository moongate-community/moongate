using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Maps;

namespace Moongate.UO.Data.Interfaces.Entities;

public interface IPositionEntity
{
    Map Map { get; set; }

    Point3D Location { get; set; }

   // void MoveTo(Point3D newLocation);
}
