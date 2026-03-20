namespace Moongate.Plugin.Abstractions.Interfaces;

/// <summary>
/// Defines the runtime context exposed to Moongate plugins.
/// </summary>
public interface IMoongatePluginRuntimeContext
{
    /// <summary>
    /// Gets the plugin id.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Gets the absolute plugin directory.
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// Gets the service resolver wrapper used by the plugin runtime.
    /// </summary>
    IMoongatePluginServiceResolver Services { get; }
}
