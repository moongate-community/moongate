using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Maps;

public class MapContent
{
    public MapType Map { get; set; }

    public int FileIndex { get; set; }

    public string Name { get; set; }

    public Point2D Size { get; set; }

    public string Rules { get; set; }

    public SeasonType Season { get; set; }

    /// <summary>
    ///     The weather profile of <c>weather.toml</c> used where no region covers a place.
    /// </summary>
    public string Weather { get; set; } = "none";

}
