using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Diagnostics;

public sealed class DiagnosticTestPlugin : IMoongatePlugin, IPluginLoaderService
{
    private readonly Container _container;
    private bool _loaded;

    public MoongatePluginData Metadata { get; } = new(
        "diagnostic-test",
        "Diagnostic Test",
        new(1, 0, 0)
    );

    public IReadOnlyList<MoongatePluginData> Plugins => [Metadata];

    public DiagnosticTestPlugin(Container container)
    {
        _container = container;
    }

    public void LoadPlugins()
    {
        if (_loaded)
        {
            return;
        }

        Register(_container);
        _loaded = true;
    }

    public void Register(Container container)
    {
        var provider = new ControlledMetricProvider("plugin_test");
        provider.Release();
        container.RegisterInstance<IMetricProvider>(provider);
    }
}
