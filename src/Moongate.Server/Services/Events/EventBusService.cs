using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Services.Events;

/// <summary>
///     Delegates injectable event operations to the container-owned event bus.
/// </summary>
public sealed class EventBusService : IEventBusService
{
    private readonly IMoongateEventBus _eventBus;

    /// <summary>
    ///     Creates an adapter over the shared event bus.
    /// </summary>
    public EventBusService(IMoongateEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
    {
        return _eventBus.PublishAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
    {
        return _eventBus.Subscribe(handler);
    }

    /// <inheritdoc />
    public IDisposable SubscribeAll(Func<IMoongateEvent, CancellationToken, Task> handler)
    {
        return _eventBus.SubscribeAll(handler);
    }
}
