using DryIoc;
using Moongate.Server.Data.Internal.Plugins;
using Moongate.Server.Interfaces.Bootstrap;
using Moongate.Server.Services.Plugins;

namespace Moongate.Server.Bootstrap.Phases;

/// <summary>
/// Bootstrap phase 2: discovers C# plugins, resolves dependencies, and runs plugin configure hooks.
/// </summary>
internal sealed class PluginConfigurationPhase : IBootstrapPhase
{
    public int Order => 2;

    public string Name => "PluginConfiguration";

    public void Configure(BootstrapContext context)
    {
        context.PluginRegistrations = new();

        var discoveryService = new PluginDiscoveryService(context.DirectoriesConfig);
        var dependencyGraphService = new PluginDependencyGraphService();
        var loaderService = new PluginLoaderService(discoveryService, dependencyGraphService);
        var loadedPlugins = loaderService.LoadPlugins();

        foreach (var loadedPlugin in loadedPlugins)
        {
            var pluginContext = new MoongatePluginContext(
                loadedPlugin.DiscoveredPlugin.PluginId,
                loadedPlugin.DiscoveredPlugin.PluginDirectory,
                context.PluginRegistrations
            );

            loadedPlugin.Instance.Configure(pluginContext);
            context.Container.RegisterInstance(loadedPlugin);
        }

        context.LoadedPlugins = loadedPlugins;
    }
}
