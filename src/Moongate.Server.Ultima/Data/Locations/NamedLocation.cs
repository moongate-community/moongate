using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Locations;

/// <summary>
///     One named place of <c>data/locations.toml</c>, which staff travels to with <c>.go</c>.
/// </summary>
public class NamedLocation
{
    /// <summary>
    ///     The map; the starting value is none, so a place without <c>map</c> is refused instead of read as Felucca.
    /// </summary>
    public MapType Map { get; set; } = (MapType)byte.MaxValue;

    /// <summary>
    ///     Where the gump files the place: the categories from the map down, joined by <c>/</c>, such as
    ///     <c>Dungeons/Covetous</c>; empty for a place listed under the map itself.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    ///     The name shown, such as <c>Entrance</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Where the traveller arrives.
    /// </summary>
    public Point3D Location { get; set; }
}
