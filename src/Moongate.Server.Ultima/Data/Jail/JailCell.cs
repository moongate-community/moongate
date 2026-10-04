using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Jail;

/// <summary>
///     One cell of <c>data/jail.toml</c>: the number the gump shows and where a prisoner arrives.
/// </summary>
public class JailCell
{
    public int Number { get; set; }

    public Point3D Location { get; set; }
}
