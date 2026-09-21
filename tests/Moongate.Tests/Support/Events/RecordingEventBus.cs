using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.Support.Events;

public sealed class RecordingEventBus : IMoongateEventBus
{
    public Task PublishAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default)
        where TEvent : class, IMoongateEvent
        => throw new NotSupportedException();

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class, IMoongateEvent
        => throw new NotSupportedException();
}
