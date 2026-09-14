using System.Runtime.CompilerServices;

using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;

namespace Moongate.Tests.Server.Core.Events;

public sealed class MoongateEventBusTests
{
    [Fact]
    public async Task PublishAsync_MultipleHandlers_AwaitsSequentiallyAndForwardsToken()
    {
        using var cancellation = new CancellationTokenSource();
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        CancellationToken receivedToken = default;
        bus.Subscribe<MoongateStartedEvent>(async (_, token) =>
        {
            calls.Add("first:enter");
            receivedToken = token;
            firstEntered.SetResult();
            await releaseFirst.Task;
            calls.Add("first:exit");
        });
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        var publication = bus.PublishAsync(new MoongateStartedEvent(), cancellation.Token);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["first:enter"], calls);
        Assert.Equal(cancellation.Token, receivedToken);

        releaseFirst.SetResult();
        await publication;

        Assert.Equal(["first:enter", "first:exit", "second"], calls);
    }

    [Fact]
    public async Task PublishAsync_ObserverThrows_ContinuesToLaterObservers()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var laterCalls = 0;
        bus.Subscribe<MoongateStartedEvent>((_, _) => throw new IOException("observer failed"));
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            laterCalls++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new MoongateStartedEvent());

        Assert.Equal(1, laterCalls);
    }

    [Fact]
    public async Task PublishAsync_UnrelatedObserverCancellation_ContinuesToLaterObservers()
    {
        using var observerCancellation = new CancellationTokenSource();
        observerCancellation.Cancel();
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var laterCalls = 0;
        bus.Subscribe<MoongateStartedEvent>((_, _) => Task.FromCanceled(observerCancellation.Token));
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            laterCalls++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new MoongateStartedEvent());

        Assert.Equal(1, laterCalls);
    }

    [Fact]
    public async Task PublishAsync_CallerCancellation_PropagatesAndSkipsLaterObservers()
    {
        using var cancellation = new CancellationTokenSource();
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var laterCalls = 0;
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        });
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            laterCalls++;
            return Task.CompletedTask;
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => bus.PublishAsync(new MoongateStartedEvent(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public async Task PublishAsync_SubscriptionMutation_AffectsNextPublicationOnly()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var calls = new List<string>();
        IDisposable? firstSubscription = null;
        firstSubscription = bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            calls.Add("first");
            firstSubscription!.Dispose();
            bus.Subscribe<MoongateStartedEvent>((_, _) =>
            {
                calls.Add("later");
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        });
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            calls.Add("second");
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new MoongateStartedEvent());
        Assert.Equal(["first", "second"], calls);

        calls.Clear();
        await bus.PublishAsync(new MoongateStartedEvent());
        Assert.Equal(["second", "later"], calls);
    }

    [Fact]
    public async Task PublishAsync_OtherEventFromHandler_CompletesWithoutDeadlock()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var nestedCalls = 0;
        bus.Subscribe<MoongateStoppingEvent>((_, _) =>
        {
            nestedCalls++;
            return Task.CompletedTask;
        });
        bus.Subscribe<MoongateStartedEvent>((_, token) =>
            bus.PublishAsync(new MoongateStoppingEvent(), token));

        var publication = Task.Run(() => bus.PublishAsync(new MoongateStartedEvent()));
        await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, nestedCalls);
    }

    [Fact]
    public async Task PublishAsync_BaseTypeSubscription_DoesNotReceiveConcreteEvent()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var baseCalls = 0;
        bus.Subscribe<IMoongateEvent>((_, _) =>
        {
            baseCalls++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new MoongateStartedEvent());

        Assert.Equal(0, baseCalls);
    }

    [Fact]
    public async Task Subscription_Dispose_RemovesOnlyItsHandlerAndIsIdempotent()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var removedCalls = 0;
        var retainedCalls = 0;
        var removed = bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            removedCalls++;
            return Task.CompletedTask;
        });
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            retainedCalls++;
            return Task.CompletedTask;
        });

        removed.Dispose();
        removed.Dispose();
        await bus.PublishAsync(new MoongateStartedEvent());

        Assert.Equal(0, removedCalls);
        Assert.Equal(1, retainedCalls);
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();

        Assert.Throws<ArgumentNullException>(() => bus.Subscribe<MoongateStartedEvent>(null!));
    }

    [Fact]
    public async Task PublishAsync_NullMessage_Throws()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync<MoongateStartedEvent>(null!));
    }

    [Fact]
    public async Task ContainerDispose_DisposesBusReleasesHandlersAndRejectsNewWork()
    {
        var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var captured = CaptureHandlerTarget(bus);

        container.Dispose();
        ForceCollection();

        Assert.False(captured.IsAlive);
        Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<MoongateStartedEvent>((_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bus.PublishAsync(new MoongateStartedEvent()));
    }

    [Fact]
    public async Task ContainerDispose_InFlightPublication_UsesCapturedHandlersWithoutWaiting()
    {
        var container = new Container();
        container.RegisterMoongateEventBus();
        var bus = container.Resolve<IMoongateEventBus>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterCalls = 0;
        bus.Subscribe<MoongateStartedEvent>(async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        });
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            laterCalls++;
            return Task.CompletedTask;
        });
        var publication = bus.PublishAsync(new MoongateStartedEvent());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        container.Dispose();
        Assert.False(publication.IsCompleted);

        release.SetResult();
        await publication;
        Assert.Equal(1, laterCalls);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CaptureHandlerTarget(IMoongateEventBus bus)
    {
        var target = new object();
        var reference = new WeakReference(target);
        bus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            GC.KeepAlive(target);
            return Task.CompletedTask;
        });
        return reference;
    }

    private static void ForceCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
