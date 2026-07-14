using Moongate.Core.Geometry;
using Moongate.Core.Types;

namespace Moongate.Persistence.Interfaces;

public interface IPositionEntity
{
    int MapId { get; set; }

    Point3D Position { get; set; }

    DirectionType Direction { get; set; }
}
