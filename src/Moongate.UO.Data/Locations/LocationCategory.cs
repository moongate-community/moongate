namespace Moongate.UO.Data.Locations;

/// <summary>
/// A node in the travel/location tree: named, with optional sub-categories and leaf locations.
/// A facet root is a top-level category.
/// </summary>
public sealed class LocationCategory
{
    public string Name { get; set; } = string.Empty;
    public List<LocationCategory> Categories { get; set; } = [];
    public List<LocationEntry> Locations { get; set; } = [];
}
