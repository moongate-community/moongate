using DryIoc;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Plugins;

namespace Moongate.Server.Core.Extensions;

/// <summary>Provides explicit plugin registration for the server container.</summary>
public static class PluginContainerExtensions
{
    extension(Container container)
    {
        public Container RegisterMoongatePlugin<TPlugin>()
            where TPlugin : class, IMoongatePlugin, new()
        {
            ArgumentNullException.ThrowIfNull(container);

            return container.RegisterMoongatePlugin(new TPlugin());
        }

        public Container RegisterMoongatePlugin(IMoongatePlugin plugin)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(plugin);

            return container.RegisterMoongatePlugins(plugin);
        }

        public Container RegisterMoongatePlugins(params IMoongatePlugin[] plugins)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(plugins);

            if (!container.IsRegistered<MoongatePluginRegistry>())
            {
                container.RegisterInstance(new MoongatePluginRegistry(container));
            }

            container.Resolve<MoongatePluginRegistry>().Register(plugins);

            return container;
        }

        /// <summary>Registers an internal plugin and its services in the shared plugin registry.</summary>
        /// <typeparam name="TPlugin">The plugin type with a public parameterless constructor.</typeparam>
        /// <returns>The container for chaining further registrations.</returns>
        public Container RegisterPlugin<TPlugin>()
            where TPlugin : class, IMoongatePlugin, new()
            => container.RegisterMoongatePlugin<TPlugin>();
    }
}
