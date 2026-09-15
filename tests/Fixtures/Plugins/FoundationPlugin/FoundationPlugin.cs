using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;

namespace Moongate.Tests.Fixtures.Plugins.FoundationPlugin;

public sealed class FoundationPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new("loader.foundation", "Foundation", new Version(1, 0));

    public void Register(Container container)
    {
        container.Resolve<List<string>>().Add("foundation:register");
    }
}
