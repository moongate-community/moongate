using DryIoc;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Tests.Server.Plugins;

/// <summary>Proves the sweep skips types it could never activate.</summary>
public abstract class AbstractPlugin : ISquidStdPlugin
{
    public abstract PluginMetadata Metadata { get; }

    public abstract void Configure(IContainer container, PluginContext context);
}
