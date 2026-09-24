namespace Moongate.Server.Core.Interfaces.Events;

/// <summary>Provides ordered, awaited publication of transient Moongate events.</summary>
public interface IMoongateEventBus
{
    /// <summary>Publishes an event to handlers registered for its exact type, then to every catch-all handler.</summary>
    Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent;

    /// <summary>Registers a handler for the exact event type.</summary>
    /// <returns>An idempotent token that removes this registration when disposed.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent;

    /// <summary>Registers a handler invoked for every published event, regardless of its type.</summary>
    /// <returns>An idempotent token that removes this registration when disposed.</returns>
    IDisposable SubscribeAll(Func<IMoongateEvent, CancellationToken, Task> handler);
}
