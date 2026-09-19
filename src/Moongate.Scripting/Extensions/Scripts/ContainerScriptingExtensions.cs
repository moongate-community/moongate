using DryIoc;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;

namespace Moongate.Scripting.Extensions.Scripts;

public static class ContainerScriptingExtensions
{
    extension(Container container)
    {
        /// <summary>Publishes a module class to Lua and registers it as a singleton so it can take dependencies.</summary>
        public Container RegisterScriptModule<TModule>()
            where TModule : class
        {
            ArgumentNullException.ThrowIfNull(container);
            GetRegistry(container).AddModule(typeof(TModule));
            container.Register<TModule>(Reuse.Singleton);

            return container;
        }

        /// <summary>Publishes an enum as a read-only global table even if no module signature mentions it.</summary>
        public Container RegisterScriptEnum<TEnum>()
            where TEnum : struct, Enum
        {
            ArgumentNullException.ThrowIfNull(container);
            GetRegistry(container).AddEnum(typeof(TEnum));

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
