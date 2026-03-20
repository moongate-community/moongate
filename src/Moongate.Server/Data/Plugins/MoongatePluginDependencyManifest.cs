namespace Moongate.Server.Data.Plugins;

/// <summary>
/// Represents a plugin dependency declaration from a manifest.
/// </summary>
public sealed record class MoongatePluginDependencyManifest
{
    /// <summary>
    /// Gets or sets the dependency plugin id.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets or sets the optional version range constraint.
    /// </summary>
    public string? VersionRange { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the dependency is optional.
    /// </summary>
    public bool Optional { get; init; }
}
