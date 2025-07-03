namespace Moongate.UO.Data.MegaCliloc;

/// <summary>
/// Represents a single cliloc property
/// </summary>
public class MegaClilocProperty
{
    /// <summary>
    /// Cliloc ID for this property
    /// </summary>
    public uint ClilocId { get; set; }

    /// <summary>
    /// Optional text to be inserted into the cliloc
    /// </summary>
    public string? Text { get; set; }
}
