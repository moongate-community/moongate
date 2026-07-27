using Moongate.Server.Abstractions.Data.Plugins;

namespace Moongate.Server.Abstractions.Interfaces.Plugins;

/// <summary>
/// Every plugin the bootstrap actually activated.
/// <para>
/// SquidStd keeps no registry of its own — <c>PluginCollectionBuilder</c> consumes plugins and gives
/// nothing back — so the catalogue is recorded where plugins are added rather than inferred by scanning
/// loaded assemblies. That distinction is load-bearing: a plugin registered behind a configuration flag
/// stays loaded in the process even when the flag switches it off, so a scan would report it as running.
/// </para>
/// </summary>
public interface IPluginCatalog
{
    /// <summary>The activated plugins, in the order they were recorded.</summary>
    IReadOnlyList<PluginDescriptor> Plugins { get; }
}
