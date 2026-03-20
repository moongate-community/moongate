using Moongate.Server.Data.Internal.Plugins;

namespace Moongate.Server.Interfaces.Services.Plugins;

/// <summary>
/// Loads plugin assemblies and entry points in dependency order.
/// </summary>
internal interface IPluginLoaderService
{
    /// <summary>
    /// Discovers, orders, and loads plugins.
    /// </summary>
    /// <returns>The loaded plugins in dependency order.</returns>
    IReadOnlyList<LoadedPlugin> LoadPlugins();
}
