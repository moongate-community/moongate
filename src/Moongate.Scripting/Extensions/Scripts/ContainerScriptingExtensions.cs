using DryIoc;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Server.Core.Interfaces.Events;

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

        /// <summary>
        ///     Publishes a bus event to Lua: scripts subscribe with <c>events.on(name, fn)</c> and receive the table
        ///     <paramref name="map" /> builds. Events are notifications; a script cannot veto them.
        /// </summary>
        /// <typeparam name="TEvent">
        ///     The bus event type; each type is published under one name only.
        /// </typeparam>
        /// <param name="name">
        ///     The snake_case name scripts subscribe with.
        /// </param>
        /// <param name="map">
        ///     Builds the values Lua receives: strings, booleans, numbers, enums (sent as numbers) or null. It runs on the
        ///     publishing thread, so it must only read the event.
        /// </param>
        /// <returns>
        ///     The same container, for chaining.
        /// </returns>
        public Container AddScriptEvent<TEvent>(string name, Func<TEvent, IReadOnlyDictionary<string, object?>> map)
            where TEvent : class, IMoongateEvent
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(map);
            GetRegistry(container)
                .AddEvent(
                    new(
                        name,
                        typeof(TEvent),
                        (bus, deliver) => bus.Subscribe<TEvent>((evt, _) =>
                            {
                                deliver(() => map(evt));

                                return Task.CompletedTask;
                            }
                        )
                    )
                );

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
