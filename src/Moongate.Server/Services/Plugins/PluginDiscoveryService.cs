using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Internal.Plugins;
using Moongate.Server.Data.Plugins;
using Moongate.Server.Interfaces.Services.Plugins;
using Moongate.Server.Json;
using Serilog;

namespace Moongate.Server.Services.Plugins;

/// <summary>
/// Discovers plugin manifests from the configured plugins directory.
/// </summary>
public sealed class PluginDiscoveryService : IPluginDiscoveryService
{
    private const string ManifestFileName = "manifest.json";
    private readonly ILogger _logger = Log.ForContext<PluginDiscoveryService>();
    private readonly DirectoriesConfig _directoriesConfig;

    public PluginDiscoveryService(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    /// <inheritdoc />
    public IReadOnlyList<DiscoveredPlugin> DiscoverPlugins()
    {
        var pluginsDirectory = _directoriesConfig[DirectoryType.Plugins];

        _logger.Information("Discovering plugins in {PluginsDirectory}", pluginsDirectory);

        if (!Directory.Exists(pluginsDirectory))
        {
            return [];
        }

        var discoveredPlugins = new List<DiscoveredPlugin>();

        foreach (
            var pluginDirectory in Directory.EnumerateDirectories(pluginsDirectory)
                                            .OrderBy(static path => path, StringComparer.Ordinal)
        )
        {
            var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = LoadManifest(manifestPath);
            ValidateManifest(manifest, manifestPath);

            discoveredPlugins.Add(new(manifest.Id!, pluginDirectory, manifestPath, manifest));

            _logger.Information("Discovered plugin {PluginId} at {ManifestPath}", manifest.Id, manifestPath);
        }

        _logger.Information(
            "Discovered {PluginCount} plugins in {PluginsDirectory}",
            discoveredPlugins.Count,
            pluginsDirectory
        );

        return discoveredPlugins;
    }

    private static MoongatePluginManifest LoadManifest(string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            MoongatePluginJsonContext.Default.MoongatePluginManifest
        );

        if (manifest is null)
        {
            throw new InvalidOperationException($"Plugin manifest '{manifestPath}' is invalid or empty.");
        }

        return manifest;
    }

    private static void ValidateManifest(MoongatePluginManifest manifest, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidOperationException($"Plugin manifest '{manifestPath}' is missing 'id'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidOperationException($"Plugin manifest '{manifestPath}' is missing 'entryAssembly'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            throw new InvalidOperationException($"Plugin manifest '{manifestPath}' is missing 'entryType'.");
        }
    }
}
