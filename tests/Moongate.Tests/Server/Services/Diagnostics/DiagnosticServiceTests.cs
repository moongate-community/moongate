using DryIoc;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Types.Diagnostics;
using Moongate.Server.Services.Diagnostics;
using Moongate.Server.Services.Events;
using Moongate.Tests.TestSupport.Diagnostics;

namespace Moongate.Tests.Server.Services.Diagnostics;

public sealed class DiagnosticServiceTests
{
    public static TheoryData<MetricSample[]> InvalidSamples
        => new()
        {
            new[] { Sample(), Sample() },
            new[] { Sample(), Sample("Bad") },
            new[] { Sample(), Sample("a.b") },
            new[] { Sample(), Sample("é") },
            new[] { Sample(), Sample("1bad") },
            new[] { Sample(), Sample("") },
            new[] { Sample(), Sample("other", double.NaN) },
            new[] { Sample(), Sample("other", double.PositiveInfinity) },
            new[] { Sample(), Sample("other", double.NegativeInfinity) },
            new[] { Sample(), new MetricSample("other", 1, " ", DiagnosticMetricType.Gauge) },
            new[] { Sample(), new MetricSample("other", -1, "count", DiagnosticMetricType.Counter) },
            new[] { Sample(), new MetricSample("other", 1, "count", (DiagnosticMetricType)99) },
            new[] { Sample(), null! }
        };

    [Theory, MemberData(nameof(InvalidSamples))]
    public async Task CollectAsync_InvalidPayloadDiscardsEntireProvider(MetricSample[] samples)
    {
        var invalid = new DelegateMetricProvider("invalid", _ => ValueTask.FromResult<IReadOnlyList<MetricSample>>(samples));
        var good = new ControlledMetricProvider();
        good.Release();
        using var fixture = new DiagnosticServiceFixture([invalid, good]);
        await fixture.Service.StartAsync();
        var snapshot = await fixture.NextAsync();
        Assert.Equal(["invalid"], snapshot.FailedProviders);
        Assert.Single(snapshot.Metrics);
        Assert.Equal(42, snapshot.Metrics["test.value"].Value);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task CollectAsync_ProviderFailureIsIsolatedAndPreviousMetricsAreRemoved(bool cancellation)
    {
        var calls = 0;
        var faulty = new DelegateMetricProvider(
            "faulty",
            _ =>
            {
                if (++calls == 1)
                {
                    return ValueTask.FromResult<IReadOnlyList<MetricSample>>([Sample()]);
                }

                if (cancellation)
                {
                    throw new OperationCanceledException();
                }

                throw new InvalidOperationException("provider failed");
            }
        );
        var good = new ControlledMetricProvider();
        good.Release();
        using var fixture = new DiagnosticServiceFixture([faulty, good]);
        await fixture.Service.StartAsync();
        var first = await fixture.NextAsync();
        Assert.Equal(42, first.Metrics["faulty.value"].Value);
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        var second = await fixture.NextAsync();
        Assert.Equal(["faulty"], second.FailedProviders);
        Assert.False(second.Metrics.ContainsKey("faulty.value"));
        Assert.Equal(42, second.Metrics["test.value"].Value);
        Assert.Equal(42, first.Metrics["faulty.value"].Value);
        Assert.Empty(first.FailedProviders);
    }

    [Fact]
    public async Task CollectAsync_SnapshotUsesEndTimeAndMonotonicDuration()
    {
        var provider = new ControlledMetricProvider();
        using var fixture = new DiagnosticServiceFixture([provider]);
        await fixture.Service.StartAsync();
        await provider.WaitForEntryAsync();
        fixture.Time.Tick(TimeSpan.FromSeconds(2));
        provider.Release();
        var first = await fixture.NextAsync();
        Assert.Equal(1, first.Sequence);
        Assert.Equal(new(2026, 9, 18, 12, 0, 2, TimeSpan.Zero), first.CollectedAt);
        Assert.Equal(TimeSpan.FromSeconds(2), first.CollectionDuration);
        var second = await fixture.NextAsync();
        Assert.Equal(2, second.Sequence);
        Assert.Equal(TimeSpan.Zero, second.CollectionDuration);
    }

    [Fact]
    public void Constructor_DuplicateProviderName_FailsBeforeCollection()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var first = new ControlledMetricProvider();
        var second = new ControlledMetricProvider();
        Assert.Throws<ArgumentException>(() => new DiagnosticService(
                [first, second],
                new(),
                new EventBusService(container.Resolve<IMoongateEventBus>()),
                new DiagnosticTimeProvider()
            )
        );
        Assert.Equal(0, first.Calls + second.Calls);
    }

    [Fact]
    public async Task Constructor_FreezesProviderListAndNames()
    {
        var provider = new DelegateMetricProvider(
            "original",
            _ => ValueTask.FromResult<IReadOnlyList<MetricSample>>([Sample()])
        );
        var providers = new List<IMetricProvider> { provider };
        using var fixture = new DiagnosticServiceFixture(providers);
        providers.Clear();
        provider.ProviderName = "changed";
        await fixture.Service.StartAsync();
        var snapshot = await fixture.NextAsync();
        Assert.Equal(42, snapshot.Metrics["original.value"].Value);
        Assert.Single(snapshot.Metrics);
    }

    [Theory, InlineData(""), InlineData(" "), InlineData("Bad"), InlineData("1bad"), InlineData("é"), InlineData("a.b"),
     InlineData("a-b")]
    public void Constructor_InvalidProviderName_FailsBeforeCollection(string name)
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var provider = new ControlledMetricProvider(name);
        Assert.Throws<ArgumentException>(() => new DiagnosticService(
                [provider],
                new(),
                new EventBusService(container.Resolve<IMoongateEventBus>()),
                new DiagnosticTimeProvider()
            )
        );
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Dispose_WaitsForWorkerAndLeavesProviderOwnedByCaller()
    {
        var entered = Signal();
        var cancelled = Signal();
        var finish = Signal();
        var lifetimeAlive = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new DelegateMetricProvider(
            "waiting",
            async token =>
            {
                entered.TrySetResult();

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    cancelled.TrySetResult();
                    await finish.Task;

                    // The lifetime source must remain usable until worker cleanup finishes.
                    lifetimeAlive.TrySetResult(token.WaitHandle.WaitOne(0));
                }

                return [Sample()];
            }
        );
        using var fixture = new DiagnosticServiceFixture([provider]);
        await fixture.Service.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposal = Task.Run(() => fixture.Service.Dispose());

        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(disposal.IsCompleted);
        }
        finally
        {
            finish.TrySetResult();
        }

        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await lifetimeAlive.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(fixture.Service.GetSnapshot());
        Assert.False(provider.IsDisposed);
        fixture.Service.Dispose();
    }

    [Fact]
    public async Task Observer_ReentrantStopIsRejectedWithoutStopping()
    {
        var provider = new ControlledMetricProvider();
        provider.Release();
        using var fixture = new DiagnosticServiceFixture([provider]);
        var rejected = Signal();
        using var subscription = fixture.Bus.Subscribe<DiagnosticSnapshotCollectedEvent>(async (_, _) =>
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.StopAsync());
                rejected.TrySetResult();
            }
        );
        await fixture.Service.StartAsync();
        await rejected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.NextAsync();
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        Assert.Equal(2, (await fixture.NextAsync()).Sequence);
        await fixture.Service.StopAsync();
    }

    [Fact]
    public async Task PublishAsync_FailingObserverDoesNotStopSubsequentCycles()
    {
        var provider = new ControlledMetricProvider();
        provider.Release();
        using var fixture = new DiagnosticServiceFixture([provider]);
        using var failing =
            fixture.Bus.Subscribe<DiagnosticSnapshotCollectedEvent>((_, _) =>
                throw new InvalidOperationException("observer failed")
            );
        await fixture.Service.StartAsync();
        Assert.Equal(1, (await fixture.NextAsync()).Sequence);
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        var second = await fixture.NextAsync();
        Assert.Equal(2, second.Sequence);
        Assert.Equal(42, second.Metrics["test.value"].Value);
        Assert.Empty(second.FailedProviders);
    }

    [Fact]
    public async Task StartAsync_ConcurrentCallsShareOneWorker()
    {
        var provider = new ControlledMetricProvider();
        using var fixture = new DiagnosticServiceFixture([provider]);
        var starts = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() =>
                        new[] { fixture.Service.StartAsync() }
                    )
                )
        );
        Assert.All(starts, start => Assert.Same(starts[0][0], start[0]));
        await Task.WhenAll(starts.Select(start => start[0]));
        await provider.WaitForEntryAsync();
        provider.Release();
        Assert.Equal(42, (await fixture.NextAsync()).Metrics["test.value"].Value);
        Assert.Equal(1, fixture.Time.TimerCount);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task StartAsync_DisabledDoesNotCreateTimerOrSnapshot()
    {
        var provider = new ControlledMetricProvider();
        using var fixture = new DiagnosticServiceFixture([provider], new() { Enabled = false });
        await fixture.Service.StartAsync();
        fixture.Time.Tick(TimeSpan.FromDays(1));
        await fixture.Service.StopAsync();
        Assert.Null(fixture.Service.GetSnapshot());
        Assert.Equal(0, fixture.Time.TimerCount);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task StartAsync_InvalidOptionsEvenWhenDisabled_FaultsSharedStartup()
    {
        using var fixture = new DiagnosticServiceFixture(
            [],
            new() { Enabled = false, Interval = TimeSpan.Zero }
        );
        var start = fixture.Service.StartAsync();
        Assert.Same(start, fixture.Service.StartAsync());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => start);
        await fixture.Service.StopAsync();
    }

    [Fact]
    public async Task StartAsync_PublishesSnapshotBeforeEventAndReadsDoNotCollect()
    {
        var provider = new ControlledMetricProvider();
        using var fixture = new DiagnosticServiceFixture([provider]);
        var received = new TaskCompletionSource<DiagnosticSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = fixture.Bus.Subscribe<DiagnosticSnapshotCollectedEvent>((message, _) =>
            {
                Assert.Same(message.Snapshot, fixture.Service.GetSnapshot());
                received.TrySetResult(message.Snapshot);

                return Task.CompletedTask;
            }
        );
        Assert.Null(fixture.Service.GetSnapshot());
        await fixture.Service.StartAsync();
        provider.Release();
        var snapshot = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(42d, snapshot.Metrics["test.value"].Value);
        Assert.Same(snapshot, fixture.Service.GetSnapshot());
        Assert.Same(snapshot, fixture.Service.GetSnapshot());
        Assert.Equal(1, provider.Calls);
        await fixture.Service.StopAsync();
        Assert.Same(snapshot, fixture.Service.GetSnapshot());
    }

    [Fact]
    public async Task StopAsync_AfterCollectionIsTerminalAndNoFurtherEventsPublish()
    {
        var provider = new ControlledMetricProvider();
        provider.Release();
        using var fixture = new DiagnosticServiceFixture([provider]);
        await fixture.Service.StartAsync();
        var snapshot = await fixture.NextAsync();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => fixture.Service.StopAsync())));
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        Assert.Same(snapshot, fixture.Service.GetSnapshot());
        Assert.False(fixture.HasPendingSnapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.StartAsync());
    }

    [Fact]
    public async Task StopAsync_BeforeStartIsTerminalAndDisposeIsIdempotent()
    {
        using var fixture = new DiagnosticServiceFixture([]);
        var stop = fixture.Service.StopAsync();
        Assert.Same(stop, fixture.Service.StopAsync());
        await stop;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.StartAsync());
        fixture.Service.Dispose();
        fixture.Service.Dispose();
        await fixture.Service.StopAsync();
        Assert.Equal(0, fixture.Time.TimerCount);
    }

    [Fact]
    public async Task StopAsync_DuringCollectionCancelsAndWaitsForProviderCleanup()
    {
        var entered = Signal();
        var cancelled = Signal();
        var finish = Signal();
        var provider = new DelegateMetricProvider(
            "waiting",
            async token =>
            {
                entered.SetResult();

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    cancelled.SetResult();
                    await finish.Task;
                }

                return [Sample()];
            }
        );
        using var fixture = new DiagnosticServiceFixture([provider]);
        await fixture.Service.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = fixture.Service.StopAsync();

        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stop.IsCompleted);
            Assert.Same(stop, fixture.Service.StopAsync());
        }
        finally
        {
            finish.TrySetResult();
        }

        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(fixture.Service.GetSnapshot());
        Assert.False(provider.IsDisposed);
        Assert.False(fixture.HasPendingSnapshot);
    }

    [Fact]
    public async Task StopAsync_UnexpectedWorkerFailureIsObserved()
    {
        var fixture = new DiagnosticServiceFixture([]);
        var failure = new InvalidOperationException("clock infrastructure failure");
        fixture.Time.TimestampFailure = failure;
        await fixture.Service.StartAsync();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.StopAsync());
        Assert.Same(failure, observed);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.Dispose()));
    }

    [Fact]
    public async Task Tick_DuringBlockedCollectionCoalescesWithoutOverlap()
    {
        var provider = new ControlledMetricProvider();
        using var fixture = new DiagnosticServiceFixture([provider]);
        await fixture.Service.StartAsync();
        await provider.WaitForEntryAsync();
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        provider.Release();
        var first = await fixture.NextAsync();
        var second = await fixture.NextAsync();
        await fixture.Service.StopAsync();
        Assert.Equal(42, first.Metrics["test.value"].Value);
        Assert.Equal(42, second.Metrics["test.value"].Value);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, provider.MaximumConcurrency);
        Assert.False(fixture.HasPendingSnapshot);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Worker_ReentrantStopOrDisposeIsRejectedWithoutStopping(bool dispose)
    {
        DiagnosticService? service = null;
        var rejection = Signal();
        var provider = new DelegateMetricProvider(
            "reentrant",
            async _ =>
            {
                if (dispose)
                {
                    Assert.Throws<InvalidOperationException>(() => service!.Dispose());
                }
                else
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => service!.StopAsync());
                }

                rejection.TrySetResult();

                return [Sample()];
            }
        );
        using var fixture = new DiagnosticServiceFixture([provider]);
        service = fixture.Service;
        await service.StartAsync();
        await rejection.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(42, (await fixture.NextAsync()).Metrics["reentrant.value"].Value);
        fixture.Time.Tick(TimeSpan.FromSeconds(5));
        Assert.Equal(2, (await fixture.NextAsync()).Sequence);
        await service.StopAsync();
    }

    private static MetricSample Sample(string name = "value", double value = 42)
    {
        return new(name, value, "count", DiagnosticMetricType.Gauge);
    }

    private static TaskCompletionSource Signal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
