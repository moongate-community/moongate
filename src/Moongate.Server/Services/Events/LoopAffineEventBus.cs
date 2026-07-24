using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Interfaces.Events;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Server.Services.Events;

/// <summary>
/// Decorates the event bus to make the single-writer invariant structural. An
/// <see cref="ILoopAffineEvent" /> published off the game loop is marshalled onto it; a handler
/// subscribed to one is wrapped so it throws if it runs off the loop or does not complete
/// synchronously (i.e. it went async and its continuation left the loop). In production the inner
/// bus isolates that throw into a logged error, so a bad handler is loud but cannot take down the
/// loop.
/// </summary>
public sealed class LoopAffineEventBus : IEventBus
{
    private readonly IEventBus _inner;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly ILoopThread _loopThread;

    public LoopAffineEventBus(IEventBus inner, IMainThreadDispatcher dispatcher, ILoopThread loopThread)
    {
        _inner = inner;
        _dispatcher = dispatcher;
        _loopThread = loopThread;
    }

    public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        if (eventData is ILoopAffineEvent && !_loopThread.IsOnLoopThread)
        {
            _dispatcher.Post(() => _inner.Publish(eventData));

            return;
        }

        _inner.Publish(eventData);
    }

    public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        if (eventData is ILoopAffineEvent && !_loopThread.IsOnLoopThread)
        {
            _dispatcher.Post(() => _inner.Publish(eventData));

            return Task.CompletedTask;
        }

        return _inner.PublishAsync(eventData, cancellationToken);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IEvent
    {
        if (!typeof(ILoopAffineEvent).IsAssignableFrom(typeof(TEvent)))
        {
            return _inner.Subscribe(handler);
        }

        return _inner.Subscribe<TEvent>((evt, ct) =>
            {
                if (!_loopThread.IsOnLoopThread)
                {
                    throw new InvalidOperationException(
                        $"Loop-affine event {typeof(TEvent).Name} was handled off the game-loop thread."
                    );
                }

                var task = handler(evt, ct);

                if (!task.IsCompleted)
                {
                    throw new InvalidOperationException(
                        $"Loop-affine handler for {typeof(TEvent).Name} did not complete synchronously; it went async off the loop."
                    );
                }

                return task;
            }
        );
    }

    public IDisposable RegisterListener<TEvent>(IEventListener<TEvent> listener) where TEvent : IEvent
        => _inner.RegisterListener(listener);
}
