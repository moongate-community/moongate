using DryIoc;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Services.Events.Internal;

namespace Moongate.Server.Core.Extensions;

/// <summary>
///     Provides container-owned registration and subscription for Moongate events.
/// </summary>
public static class ContainerEventExtensions
{
    extension(Container container)
    {
        /// <summary>
        ///     Registers an awaited handler for the exact event type for the container lifetime.
        /// </summary>
        public Container OnEvent<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : class, IMoongateEvent
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(handler);

            container.RegisterMoongateEventBus();
            container.Resolve<IMoongateEventBus>().Subscribe(handler);

            return container;
        }

        /// <summary>
        ///     Ensures the container has one singleton Moongate event bus.
        /// </summary>
        public Container RegisterMoongateEventBus()
        {
            ArgumentNullException.ThrowIfNull(container);

            if (!container.IsRegistered<IMoongateEventBus>())
            {
                container.Register<IMoongateEventBus, MoongateEventBus>(Reuse.Singleton);
            }

            return container;
        }
    }
}
