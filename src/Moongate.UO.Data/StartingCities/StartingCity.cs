using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.StartingCities;

/// <summary>
/// A character-creation starting city: the displayed city name, the specific building (inn), a cliloc
/// description, and its location on a map facet. The dataset order is the index sent to the client.
/// </summary>
public sealed class StartingCity
{
    public string City { get; set; }
    public string Building { get; set; }
    public int Description { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public MapType Map { get; set; }

    public override string ToString()
        => $"{City} ({Building}) at {X},{Y},{Z} on {Map}";
}
