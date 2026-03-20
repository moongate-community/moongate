using Moongate.Server.Data.Internal.Plugins;

namespace Moongate.Server.Interfaces.Services.Plugins;

/// <summary>
/// Discovers plugin manifests from the configured plugins directory.
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>
    /// Discovers valid plugin manifests from the configured plugins directory.
    /// </summary>
    /// <returns>The discovered plugins.</returns>
    IReadOnlyList<DiscoveredPlugin> DiscoverPlugins();
}
