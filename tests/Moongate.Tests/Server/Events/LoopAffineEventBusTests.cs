using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Services.Events;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Events;

public class LoopAffineEventBusTests
{
    private sealed record LoopEvent : ILoopAffineEvent;

    private sealed record PlainEvent : IEvent;

    // Records what inner.Publish received, and captures the last handler passed to Subscribe so a
    // test can invoke it directly — bypassing the real bus, which would swallow a thrown guard.
    private sealed class CapturingEventBus : IEventBus
    {
        public List<IEvent> Published { get; } = [];
        public Delegate? LastHandler { get; private set; }

        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
            => Published.Add(eventData);

        public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
            where TEvent : IEvent
        {
            Published.Add(eventData);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IEvent
        {
            LastHandler = handler;
            return new Noop();
        }

        public IDisposable RegisterListener<TEvent>(IEventListener<TEvent> listener) where TEvent : IEvent
            => new Noop();

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public void Publish_LoopAffineEventOffLoop_IsPostedNotRunInline()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: false));

        bus.Publish(new LoopEvent());

        Assert.Empty(inner.Published);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.DrainPending();

        Assert.Single(inner.Published);
    }

    [Fact]
    public void Publish_LoopAffineEventOnLoop_RunsInline()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: true));

        bus.Publish(new LoopEvent());

        Assert.Single(inner.Published);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void Publish_PlainEventOffLoop_PassesThrough()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: false));

        bus.Publish(new PlainEvent());

        Assert.Single(inner.Published);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void Subscribe_LoopAffineHandlerOffLoop_Throws()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: false));

        bus.Subscribe<LoopEvent>((_, _) => Task.CompletedTask);
        var wrapped = (Func<LoopEvent, CancellationToken, Task>)inner.LastHandler!;

        Assert.Throws<InvalidOperationException>(() => wrapped(new LoopEvent(), default).GetAwaiter().GetResult());
    }

    [Fact]
    public void Subscribe_LoopAffineHandlerGoesAsync_Throws()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: true));

        // A never-completed task models a handler that did not finish synchronously (it went async),
        // deterministically — unlike Task.Yield(), whose thread-pool continuation can complete before
        // the guard checks IsCompleted, racing the assertion.
        var pending = new TaskCompletionSource();
        bus.Subscribe<LoopEvent>((_, _) => pending.Task);
        var wrapped = (Func<LoopEvent, CancellationToken, Task>)inner.LastHandler!;

        // .GetAwaiter().GetResult() makes the lambda void-returning (not Task-returning), so it binds
        // to Assert.Throws<T>(Action) instead of the xUnit-analyzer-flagged Func<Task> overload. The
        // guard throws synchronously before ever returning a task, so GetResult() is never reached —
        // this is equivalent to a plain synchronous throw, just in a shape the analyzer accepts.
        Assert.Throws<InvalidOperationException>(() => wrapped(new LoopEvent(), default).GetAwaiter().GetResult());
    }

    [Fact]
    public void Subscribe_LoopAffineHandlerOnLoopSynchronous_RunsAndPassesThrough()
    {
        var inner = new CapturingEventBus();
        var dispatcher = new MainThreadDispatcherService();
        var bus = new LoopAffineEventBus(inner, dispatcher, new StubLoopThread(onLoop: true));
        var ran = false;

        bus.Subscribe<LoopEvent>((_, _) =>
            {
                ran = true;
                return Task.CompletedTask;
            }
        );
        var wrapped = (Func<LoopEvent, CancellationToken, Task>)inner.LastHandler!;
        wrapped(new LoopEvent(), default).GetAwaiter().GetResult();

        Assert.True(ran);
    }
}
