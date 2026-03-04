namespace Moongate.UO.Data.Json.Locations;

/// <summary>
/// Represents a named location entry from map location JSON files.
/// </summary>
public sealed class JsonLocationDefinition
{
    /// <summary>
    /// Gets or sets the display name of the location.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets world coordinates as [x, y, z].
    /// </summary>
    public int[] Location { get; set; } = [];
}
