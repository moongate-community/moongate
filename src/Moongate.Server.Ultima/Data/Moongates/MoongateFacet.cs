using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Moongates;

/// <summary>
///     The public moongates of one map in <c>data/moongates.toml</c>: the page of the gump and its cities.
/// </summary>
public class MoongateFacet
{
    public MapType Map { get; set; }

    /// <summary>
    ///     The client text of the map's name in the gump.
    /// </summary>
    public int Cliloc { get; set; }

    /// <summary>
    ///     The client text of the map's name as the gump shows it for the open page.
    /// </summary>
    public int SelectedCliloc { get; set; }

    /// <summary>
    ///     The cities, in the order the gump lists them.
    /// </summary>
    public List<MoongateDestination> Destination { get; set; } = [];
}
