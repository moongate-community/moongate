namespace Moongate.Server.Data.Plugins;

/// <summary>
/// Represents a plugin manifest loaded from <c>manifest.json</c>.
/// </summary>
public sealed record class MoongatePluginManifest
{
    /// <summary>
    /// Gets or sets the unique plugin id.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Gets or sets the plugin display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets or sets the plugin authors.
    /// </summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>
    /// Gets or sets the optional plugin description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the entry assembly path relative to the plugin directory.
    /// </summary>
    public string? EntryAssembly { get; init; }

    /// <summary>
    /// Gets or sets the entry type name inside the entry assembly.
    /// </summary>
    public string? EntryType { get; init; }

    /// <summary>
    /// Gets or sets the plugin dependencies.
    /// </summary>
    public IReadOnlyList<MoongatePluginDependencyManifest> Dependencies { get; init; } = [];
}
