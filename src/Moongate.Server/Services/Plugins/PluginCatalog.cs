using Moongate.Server.Abstractions.Data.Plugins;
using Moongate.Server.Abstractions.Interfaces.Plugins;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server.Services.Plugins;

/// <summary>
/// The catalogue the bootstrap fills in as it activates plugins. Written once during startup and read
/// afterwards, so no synchronisation is needed.
/// </summary>
public sealed class PluginCatalog : IPluginCatalog
{
    private readonly List<PluginDescriptor> _plugins = [];

    public IReadOnlyList<PluginDescriptor> Plugins => _plugins;

    /// <summary>
    /// Records a plugin as active. A second record of an id already held is ignored, which is what makes
    /// the explicit registrations authoritative over the by-elimination sweep for directory-loaded ones.
    /// </summary>
    public void Record(ISquidStdPlugin plugin, bool isExternal)
    {
        var metadata = plugin.Metadata;

        if (_plugins.Any(recorded => recorded.Id == metadata.Id))
        {
            return;
        }

        _plugins.Add(
            new(
                metadata.Id,
                metadata.Name,
                metadata.Version,
                metadata.Author,
                metadata.Description,
                plugin.GetType().Assembly.GetName().Name ?? string.Empty,
                isExternal
            )
        );
    }
}
