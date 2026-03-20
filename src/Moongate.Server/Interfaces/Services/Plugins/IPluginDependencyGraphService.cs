using Moongate.Server.Data.Internal.Plugins;

namespace Moongate.Server.Interfaces.Services.Plugins;

/// <summary>
/// Validates plugin dependencies and returns a bootstrap-safe load order.
/// </summary>
public interface IPluginDependencyGraphService
{
    /// <summary>
    /// Orders discovered plugins according to their declared dependencies.
    /// </summary>
    /// <param name="plugins">The discovered plugins.</param>
    /// <returns>The dependency-sorted plugins.</returns>
    IReadOnlyList<DiscoveredPlugin> ResolveDependencyOrder(IReadOnlyList<DiscoveredPlugin> plugins);
}
