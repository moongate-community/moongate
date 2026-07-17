using Moongate.Server.Abstractions.Interfaces.Events;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.Events;

/// <summary>
/// Attaches every registered <see cref="IEventSubscriberRegistration" /> to the event bus at startup,
/// mirroring how the network service wires packet handlers. Subscriptions live for the process, so
/// there is nothing to undo on stop.
/// </summary>
public sealed class EventSubscriberService : IEventSubscriberService, ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<EventSubscriberService>();
    private readonly IEnumerable<IEventSubscriberRegistration> _subscribers;
    private readonly IEventBus _eventBus;

    public int Count { get; private set; }

    public EventSubscriberService(IEnumerable<IEventSubscriberRegistration> subscribers, IEventBus eventBus)
    {
        _subscribers = subscribers;
        _eventBus = eventBus;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.Subscribe(_eventBus);
            Count++;
        }

        _logger.Information("Wired {Count} event subscribers", Count);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
