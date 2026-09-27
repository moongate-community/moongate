using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.TestSupport.Events;

/// <summary>
///     Runs handlers inline, in subscription order, and records every published event.
/// </summary>
public sealed class InProcessEventBus : IMoongateEventBus
{
    private readonly List<(Type Type, Func<IMoongateEvent, CancellationToken, Task> Handler)> _handlers = [];

    public List<IMoongateEvent> Published { get; } = [];

    public async Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
    {
        Published.Add(message);

        foreach (var (type, handler) in _handlers.ToList())
        {
            if (type == typeof(TEvent))
            {
                await handler(message, cancellationToken);
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
    {
        var entry = (typeof(TEvent),
                     (Func<IMoongateEvent, CancellationToken, Task>)((message, token) => handler((TEvent)message, token)));
        _handlers.Add(entry);

        return new EventSubscription(() => _handlers.Remove(entry));
    }

    public IDisposable SubscribeAll(Func<IMoongateEvent, CancellationToken, Task> handler)
    {
        throw new NotSupportedException();
    }
}
