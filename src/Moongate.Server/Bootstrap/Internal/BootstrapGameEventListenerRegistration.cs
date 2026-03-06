using System.Diagnostics.CodeAnalysis;
using DryIoc;
using Moongate.Abstractions.Data.Internal;
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
}
