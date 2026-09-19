using DryIoc;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>IEventBusService over the container-owned bus, as EventBusService does in the server.</summary>
public sealed class EventBusAdapter : IEventBusService
{
    private readonly IMoongateEventBus _bus;

    public EventBusAdapter(Container container)
    {
        _bus = container.Resolve<IMoongateEventBus>();
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
    {
        return _bus.Subscribe(handler);
    }

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
    {
        return _bus.PublishAsync(message, cancellationToken);
    }
}
