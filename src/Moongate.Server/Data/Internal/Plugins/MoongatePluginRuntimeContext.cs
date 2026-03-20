using Moongate.Plugin.Abstractions.Interfaces;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// Runtime plugin context used after the server is fully bootstrapped.
/// </summary>
internal sealed class MoongatePluginRuntimeContext : IMoongatePluginRuntimeContext
{
    public MoongatePluginRuntimeContext(
        string pluginId,
        string pluginDirectory,
        IMoongatePluginServiceResolver services
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        ArgumentNullException.ThrowIfNull(services);

        PluginId = pluginId;
        PluginDirectory = pluginDirectory;
        Services = services;
    }

    public string PluginId { get; }

    public string PluginDirectory { get; }

    public IMoongatePluginServiceResolver Services { get; }
}
