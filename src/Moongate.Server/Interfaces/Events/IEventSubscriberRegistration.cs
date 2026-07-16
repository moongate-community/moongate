using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Interfaces.Events;

/// <summary>
/// A behavior that reacts to domain events. Implementations attach their handlers to the bus when the
/// server starts; this is the event-side twin of <c>IPacketHandlerRegistration</c>.
/// </summary>
public interface IEventSubscriberRegistration
{
    /// <summary>Attaches this subscriber's handlers to <paramref name="eventBus" />.</summary>
    void Subscribe(IEventBus eventBus);
}
