namespace Moongate.UO.Data.Json.Locations;

/// <summary>
/// Represents a hierarchical location category node.
/// </summary>
public sealed class JsonLocationCategory
{
    /// <summary>
    /// Gets or sets category name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets nested subcategories.
    /// </summary>
    public List<JsonLocationCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets direct location entries.
    /// </summary>
    public List<JsonLocationDefinition> Locations { get; set; } = [];
}
