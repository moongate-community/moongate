using DryIoc;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Tests.Server.Plugins;

/// <summary>Stands in for a plugin DLL dropped into <c>plugins/</c> — discovered, never referenced.</summary>
public sealed class DiscoverablePlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "discoverable.plugin",
            Name = "Discoverable",
            Version = new(1, 0, 0),
            Author = "squid",
            Description = "found by elimination"
        };

    public void Configure(IContainer container, PluginContext context)
    {
    }
}
