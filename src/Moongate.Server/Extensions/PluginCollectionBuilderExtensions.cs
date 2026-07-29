using Moongate.Server.Services.Plugins;
using SquidStd.Plugin;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server.Extensions;

/// <summary>
/// Adds internal plugins to the bootstrap while recording them in the <see cref="PluginCatalog" />.
/// </summary>
public static class PluginCollectionBuilderExtensions
{
    /// <summary>
    /// Activates <typeparamref name="TPlugin" />, records it in <paramref name="catalog" /> as internal, and
    /// registers the instance with <paramref name="builder" />.
    /// <para>
    /// Recorded at the point of activation rather than scanned for afterwards: a plugin behind a
    /// configuration flag keeps its assembly loaded even when the flag switches it off, so a scan of
    /// loaded assemblies would report it as running.
    /// </para>
    /// </summary>
    public static PluginCollectionBuilder AddTracked<TPlugin>(this PluginCollectionBuilder builder, PluginCatalog catalog)
        where TPlugin : ISquidStdPlugin, new()
    {
        var plugin = new TPlugin();

        catalog.Record(plugin, false);

        return builder.Add(plugin);
    }
}
