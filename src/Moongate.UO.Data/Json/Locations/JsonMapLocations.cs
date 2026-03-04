namespace Moongate.UO.Data.Json.Locations;

/// <summary>
/// Represents the root object of a map locations JSON file.
/// </summary>
public sealed class JsonMapLocations
{
    /// <summary>
    /// Gets or sets map display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets top-level categories.
    /// </summary>
    public List<JsonLocationCategory> Categories { get; set; } = [];
}
