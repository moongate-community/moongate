namespace Moongate.UO.Data.Locations;

/// <summary>A named world point (a leaf in the travel/location tree).</summary>
public sealed class LocationEntry
{
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
