using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.Support.Events;

public sealed class FaultingLifecycleEventBus : IMoongateEventBus
{
    private readonly List<string> _events;
    private readonly Exception _stoppingFailure;
    private readonly Exception _stoppedFailure;

    public FaultingLifecycleEventBus(List<string> events, Exception stoppingFailure, Exception stoppedFailure)
    {
        _events = events;
        _stoppingFailure = stoppingFailure;
        _stoppedFailure = stoppedFailure;
    }

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
    {
        if (typeof(TEvent) == typeof(MoongateStoppingEvent))
        {
            _events.Add("stopping");

            return Task.FromException(_stoppingFailure);
        }

        if (typeof(TEvent) == typeof(MoongateStoppedEvent))
        {
            _events.Add("stopped");

            return Task.FromException(_stoppedFailure);
        }

        return Task.CompletedTask;
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
    {
        throw new NotSupportedException();
    }

    public IDisposable SubscribeAll(Func<IMoongateEvent, CancellationToken, Task> handler)
    {
        throw new NotSupportedException();
    }
}
