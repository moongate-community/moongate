using Moongate.Server.Core.Data.Plugins;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Loads disk plugins into the server's shared plugin registry before service startup.</summary>
public interface IPluginLoaderService : IMoongateService
{
    /// <summary>Gets internal and disk plugins registered in dependency order.</summary>
    IReadOnlyList<MoongatePluginData> Plugins { get; }

    /// <summary>Loads and registers plugins once from the configured plugins directory.</summary>
    /// <remarks>
    /// Call during host configuration, before startup services are resolved.
    /// Successful repeated calls do nothing; a failed load requires discarding the host container.
    /// </remarks>
    void LoadPlugins();
}
