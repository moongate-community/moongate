using Moongate.Server.Data.Plugins;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// Represents a discovered plugin manifest and its source directory.
/// </summary>
public sealed record class DiscoveredPlugin(
    string PluginId,
    string PluginDirectory,
    string ManifestPath,
    MoongatePluginManifest Manifest
);
