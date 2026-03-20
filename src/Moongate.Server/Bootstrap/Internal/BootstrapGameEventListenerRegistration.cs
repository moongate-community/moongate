using System.Diagnostics.CodeAnalysis;
using DryIoc;
using Moongate.Abstractions.Data.Internal;
using Moongate.Abstractions.Extensions;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Interfaces.Services.Events;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers and subscribes generated game event listeners.
/// </summary>
internal static partial class BootstrapGameEventListenerRegistration
{
    public static void RegisterServices(Container container)
        => RegisterServicesGenerated(container);

    public static void Subscribe(Container container)
        => SubscribeGenerated(container);

    public static void RegisterServices(Container container, IEnumerable<Type> pluginListenerTypes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(pluginListenerTypes);

        foreach (var listenerType in pluginListenerTypes)
        {
            RegisterListenerService(container, listenerType);
        }
    }

    public static void Subscribe(Container container, IEnumerable<Type> pluginListenerTypes)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(pluginListenerTypes);

        foreach (var listenerType in pluginListenerTypes)
        {
            SubscribeListener(container, listenerType);
        }
    }

    private static void RegisterListener<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        TListener,
        TEvent
    >(Container container)
        where TListener : class, IGameEventListener<TEvent>
        where TEvent : IGameEvent
    {
        var gameEventBusService = container.Resolve<IGameEventBusService>();
        var listener = ResolveListener<TListener>(container);
        gameEventBusService.RegisterListener(listener);
    }

    static partial void RegisterServicesGenerated(Container container);

    private static TListener ResolveListener<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        TListener
    >(Container container) where TListener : class
    {
        if (container.IsRegistered<List<ServiceRegistrationObject>>())
        {
            var registrations = container.Resolve<List<ServiceRegistrationObject>>();

            foreach (var registration in registrations)
            {
                if (registration.ImplementationType != typeof(TListener) ||
                    !container.IsRegistered(registration.ServiceType))
                {
                    continue;
                }

                if (container.Resolve(registration.ServiceType) is TListener listener)
                {
                    return listener;
                }
            }
        }

        foreach (var interfaceType in typeof(TListener).GetInterfaces())
        {
            if (!container.IsRegistered(interfaceType))
            {
                continue;
            }

            if (container.Resolve(interfaceType) is TListener listener)
            {
                return listener;
            }
        }

        if (container.IsRegistered<TListener>())
        {
            return container.Resolve<TListener>();
        }

        throw new InvalidOperationException($"Listener type '{typeof(TListener).FullName}' is not registered in DryIoc.");
    }

    static partial void SubscribeGenerated(Container container);

    private static void RegisterListenerService(Container container, Type listenerType)
    {
        if (typeof(IMoongateService).IsAssignableFrom(listenerType))
        {
            var attribute = (RegisterGameEventListenerAttribute?)Attribute.GetCustomAttribute(
                listenerType,
                typeof(RegisterGameEventListenerAttribute)
            );
            var priority = attribute?.Priority ?? 200;

            container.RegisterMoongateService(listenerType, listenerType, priority);

            return;
        }

        if (!container.IsRegistered(listenerType))
        {
            container.Register(listenerType, Reuse.Singleton);
        }
    }

    private static void SubscribeListener(Container container, Type listenerType)
    {
        foreach (var interfaceType in listenerType.GetInterfaces())
        {
            if (!interfaceType.IsGenericType ||
                interfaceType.GetGenericTypeDefinition() != typeof(IGameEventListener<>))
            {
                continue;
            }

            var eventType = interfaceType.GetGenericArguments()[0];
            var registerMethod = typeof(BootstrapGameEventListenerRegistration)
                                 .GetMethod(
                                     nameof(RegisterListener),
                                     System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Static
                                 )!
                                 .MakeGenericMethod(listenerType, eventType);

            registerMethod.Invoke(null, [container]);
        }
    }
}
