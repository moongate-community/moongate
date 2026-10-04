using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Jail;

/// <summary>
///     The root of <c>data/jail.toml</c>: the map of the jail, where a released prisoner goes when the map it was
///     arrested on is gone, and one <c>[[cell]]</c> per cell.
/// </summary>
public class JailFile
{
    public MapType Map { get; set; } = (MapType)byte.MaxValue;

    public Point3D Release { get; set; }

    public List<JailCell> Cell { get; set; } = [];
}
