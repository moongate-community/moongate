using System.Reflection;
using Moongate.Plugin.Abstractions.Interfaces;
using Moongate.Server.Data.Internal.Plugins;
using Moongate.Server.Interfaces.Services.Plugins;
using Serilog;

namespace Moongate.Server.Services.Plugins;

/// <summary>
/// Loads plugin assemblies and instantiates their entry points.
/// </summary>
internal sealed class PluginLoaderService : IPluginLoaderService
{
    private readonly IPluginDependencyGraphService _dependencyGraphService;
    private readonly IPluginDiscoveryService _discoveryService;
    private readonly ILogger _logger = Log.ForContext<PluginLoaderService>();

    public PluginLoaderService(
        IPluginDiscoveryService discoveryService,
        IPluginDependencyGraphService dependencyGraphService
    )
    {
        ArgumentNullException.ThrowIfNull(discoveryService);
        ArgumentNullException.ThrowIfNull(dependencyGraphService);

        _discoveryService = discoveryService;
        _dependencyGraphService = dependencyGraphService;
    }

    public IReadOnlyList<LoadedPlugin> LoadPlugins()
    {
        var discoveredPlugins = _discoveryService.DiscoverPlugins();
        var orderedPlugins = _dependencyGraphService.ResolveDependencyOrder(discoveredPlugins);
        var loadedPlugins = new List<LoadedPlugin>(orderedPlugins.Count);

        foreach (var discoveredPlugin in orderedPlugins)
        {
            var entryAssemblyPath = Path.GetFullPath(
                Path.Combine(discoveredPlugin.PluginDirectory, discoveredPlugin.Manifest.EntryAssembly!)
            );

            if (!File.Exists(entryAssemblyPath))
            {
                throw new InvalidOperationException(
                    $"Plugin '{discoveredPlugin.PluginId}' entry assembly not found at '{entryAssemblyPath}'."
                );
            }

            var assembly = Assembly.LoadFrom(entryAssemblyPath);
            var entryType = assembly.GetType(discoveredPlugin.Manifest.EntryType!, false);

            if (entryType is null)
            {
                throw new InvalidOperationException(
                    $"Plugin '{discoveredPlugin.PluginId}' entry type '{discoveredPlugin.Manifest.EntryType}' was not found in '{entryAssemblyPath}'."
                );
            }

            if (!typeof(IMoongatePlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException(
                    $"Plugin '{discoveredPlugin.PluginId}' entry type '{entryType.FullName}' does not implement IMoongatePlugin."
                );
            }

            if (Activator.CreateInstance(entryType) is not IMoongatePlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Plugin '{discoveredPlugin.PluginId}' entry type '{entryType.FullName}' could not be instantiated."
                );
            }

            _logger.Information(
                "Loaded plugin {PluginId} ({PluginName}) from {AssemblyPath}",
                discoveredPlugin.PluginId,
                plugin.Name,
                entryAssemblyPath
            );

            loadedPlugins.Add(new(discoveredPlugin, assembly, entryType, plugin));
        }

        return loadedPlugins;
    }
}
