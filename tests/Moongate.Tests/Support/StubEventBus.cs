using SquidStd.Core.Interfaces.Events;

namespace Moongate.Tests.Support;

/// <summary>
/// Minimal <see cref="IEventBus" /> test double: records the events published through it and treats
/// subscriptions as no-ops. Lets systems under test that publish events run without a real bus.
/// </summary>
public sealed class StubEventBus : IEventBus
{
    private readonly List<IEvent> _published = [];

    public IReadOnlyList<IEvent> Published => _published;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose() { }
    }

    public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        => _published.Add(eventData);

    public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        _published.Add(eventData);

        return Task.CompletedTask;
    }

    public IDisposable RegisterListener<TEvent>(IEventListener<TEvent> listener) where TEvent : IEvent
        => NoopDisposable.Instance;

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IEvent
        => NoopDisposable.Instance;
}
