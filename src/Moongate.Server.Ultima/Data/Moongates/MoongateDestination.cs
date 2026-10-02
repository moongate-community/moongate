using Moongate.Core.Geometry;

namespace Moongate.Server.Ultima.Data.Moongates;

/// <summary>
///     One city of a map's public moongates in <c>data/moongates.toml</c>: where its gate stands and travellers arrive.
/// </summary>
public class MoongateDestination
{
    /// <summary>
    ///     The city, for the logs; players see the client text <see cref="Cliloc" />.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     The client text of the city's name.
    /// </summary>
    public int Cliloc { get; set; }

    /// <summary>
    ///     Where the gate stands and travellers arrive.
    /// </summary>
    public Point3D Location { get; set; }

    /// <summary>
    ///     The hue of the gate; 0 for none.
    /// </summary>
    public int Hue { get; set; }

    /// <summary>
    ///     Whether the height comes from the map instead of the z of <see cref="Location" />.
    /// </summary>
    public bool AverageZ { get; set; }
}
