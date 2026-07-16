using DryIoc;
using Moongate.Server.Interfaces.Events;

namespace Moongate.Server.Extensions;

/// <summary>
/// Registers event subscribers. Each one is collected under <see cref="IEventSubscriberRegistration" />,
/// the typed list the event subscriber service resolves and attaches to the bus at startup.
/// </summary>
public static class EventSubscriberRegistrationExtensions
{
    /// <summary>
    /// Records an event subscriber as a singleton <see cref="IEventSubscriberRegistration" /> so it is
    /// wired to the bus alongside every other subscriber.
    /// </summary>
    public static IContainer RegisterEventSubscriber<TSubscriber>(this IContainer container)
        where TSubscriber : class, IEventSubscriberRegistration
    {
        container.Register<IEventSubscriberRegistration, TSubscriber>(Reuse.Singleton);

        return container;
    }
}
