using DryIoc;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Extensions.Scripts;

/// <summary>
///     Container registrations that tell the script engine what to publish to Lua at startup. Call them before the engine
///     starts; the registry is read once, while it binds.
/// </summary>
public static class ContainerScriptingExtensions
{
    extension(Container container)
    {
        /// <summary>
        ///     Publishes an enum as a read-only global table even if no module signature mentions it.
        /// </summary>
        /// <typeparam name="TEnum">
        ///     The enum to publish; its members become the table's keys.
        /// </typeparam>
        /// <returns>
        ///     The same container, for chaining.
        /// </returns>
        public Container RegisterScriptEnum<TEnum>()
            where TEnum : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(container);
            GetRegistry(container).AddEnum(typeof(TEnum));

            return container;
        }

        /// <summary>
        ///     Publishes a module class to Lua and registers it as a singleton so it can take dependencies.
        /// </summary>
        /// <typeparam name="TModule">
        ///     A class carrying <see cref="Moongate.Scripting.Attributes.Scripts.ScriptModuleAttribute" />.
        /// </typeparam>
        /// <returns>
        ///     The same container, for chaining.
        /// </returns>
        public Container AddScriptModule<TModule>()
            where TModule : class
        {
            ArgumentNullException.ThrowIfNull(container);
            GetRegistry(container).AddModule(typeof(TModule));
            container.Register<TModule>(Reuse.Singleton);

            return container;
        }
    }

    private static IScriptModuleRegistry GetRegistry(Container container)
    {
        if (!container.IsRegistered<IScriptModuleRegistry>())
        {
            container.RegisterInstance<IScriptModuleRegistry>(new ScriptModuleRegistry());
        }

        return container.Resolve<IScriptModuleRegistry>();
    }
}
