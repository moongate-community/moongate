using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Cities;

public class StartingCityContent
{
    public string Town { get; set; }

    public string Description { get; set; }

    public Point3D Location { get; set; }

    public MapType Map { get; set; }

    public Serial Cliloc { get; set; }

}
