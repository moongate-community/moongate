using System.Globalization;
using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Diagnostics;
using Moongate.Server.Services.Diagnostics.Providers;
using Moongate.Server.Services.Events;
using Moongate.Tests.Support.Server;
using Moongate.Tests.Support.Server.Interfaces;
using Moongate.Tests.TestSupport.Diagnostics;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Diagnostics;

public sealed class DiagnosticBootstrapTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StartAsync_LoadsPluginProviderBeforeResolvingSingletonCollectorOnSharedBus()
    {
        var container = new Container();
        var time = new DiagnosticTimeProvider();
        var plugin = new DiagnosticTestPlugin(container);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services => RegisterDiagnosticHost(services, time, plugin));
        var bus = container.Resolve<IEventBusService>();
        var observed = new TaskCompletionSource<DiagnosticSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bus.Subscribe<DiagnosticSnapshotCollectedEvent>((message, _) =>
        {
            observed.TrySetResult(message.Snapshot);
            return Task.CompletedTask;
        });

        await bootstrap.StartAsync();
        var snapshot = await observed.Task.WaitAsync(Timeout);
        var first = container.Resolve<IDiagnosticService>();
        var second = container.Resolve<IDiagnosticService>();
        var providers = container.Resolve<IEnumerable<IMetricProvider>>().ToArray();
        var providersAgain = container.Resolve<IEnumerable<IMetricProvider>>().ToArray();

        Assert.Same(first, second);
        Assert.Equal(5, providers.Length);
        Assert.Equal(providers.Select(provider => provider.ProviderName),
            providersAgain.Select(provider => provider.ProviderName));
        Assert.All(providers.Zip(providersAgain), pair => Assert.Same(pair.First, pair.Second));
        Assert.Same(snapshot, first.GetSnapshot());
        Assert.Equal(42, snapshot.Metrics["plugin_test.value"].Value);
        Assert.Same(bus, container.Resolve<IEventBusService>());

        await bootstrap.StopAsync();
        time.Dispose();
    }

    [Fact]
    public async Task StartAndStopAsync_DiagnosticsRunsInsideTimerAndGameLoopLifetime()
    {
        var events = new List<string>();
        var secondCollectionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSnapshot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var provider = new DelegateMetricProvider("ordered", async token =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                Assert.Equal(["start:timer", "start:game_loop"], events);
                events.Add("start:diagnostics");
            }
            else
            {
                secondCollectionEntered.TrySetResult();
                try
                {
                    await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token);
                }
                finally
                {
                    events.Add("stop:diagnostics");
                }
            }

            return [];
        });
        var time = new DiagnosticTimeProvider();
        var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services =>
            {
                services.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(
                    new RecordingStartupService("timer", events), -900);
                services.RegisterMoongateService<ISecondaryRecordingStartupService, RecordingStartupService>(
                    new RecordingStartupService("game_loop", events), -800);
                RegisterCollector(services, time, [provider]);
                services.Resolve<IEventBusService>().Subscribe<DiagnosticSnapshotCollectedEvent>((_, _) =>
                {
                    firstSnapshot.TrySetResult();
                    return Task.CompletedTask;
                });
                return services;
            });

        await bootstrap.StartAsync();
        await firstSnapshot.Task.WaitAsync(Timeout);
        time.Tick(TimeSpan.FromSeconds(5));
        await secondCollectionEntered.Task.WaitAsync(Timeout);

        await bootstrap.StopAsync().WaitAsync(Timeout);

        Assert.Equal(
            ["start:timer", "start:game_loop", "start:diagnostics", "stop:diagnostics", "stop:game_loop", "stop:timer"],
            events);
        time.Dispose();
    }

    [Fact]
    public async Task StartAsync_LaterServiceFailureRollsBackDiagnosticsWithoutFurtherEvents()
    {
        var time = new DiagnosticTimeProvider();
        var provider = new ControlledMetricProvider();
        provider.Release();
        var snapshotCollected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = 0;
        var failure = new InvalidOperationException("controlled priority 950 failure");
        var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services =>
            {
                RegisterCollector(services, time, [provider]);
                services.Resolve<IEventBusService>().Subscribe<DiagnosticSnapshotCollectedEvent>((_, _) =>
                {
                    Interlocked.Increment(ref events);
                    snapshotCollected.TrySetResult();
                    return Task.CompletedTask;
                });
                return services.RegisterMoongateService(
                    new CallbackStartupService(
                        async () =>
                        {
                            await snapshotCollected.Task.WaitAsync(Timeout);
                            throw failure;
                        },
                        () => Task.CompletedTask
                    ),
                    950
                );
            });

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.StartAsync());
        var countAfterRollback = Volatile.Read(ref events);
        time.Tick(TimeSpan.FromSeconds(5));
        await Task.Yield();

        Assert.Same(failure, observed);
        Assert.Equal(1, countAfterRollback);
        Assert.Equal(countAfterRollback, Volatile.Read(ref events));
        Assert.Equal(1, provider.Calls);
        await bootstrap.StopAsync();
        time.Dispose();
    }

    [Fact]
    public async Task StartAsync_DisabledDiagnosticsDoesNotCreateWorkerOrSnapshot()
    {
        var time = new DiagnosticTimeProvider();
        var provider = new ControlledMetricProvider();
        var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services => RegisterCollector(
                services,
                time,
                [provider],
                new DiagnosticOptions { Enabled = false }
            ));

        await bootstrap.StartAsync();
        time.Tick(TimeSpan.FromSeconds(5));
        var diagnostics = container.Resolve<IDiagnosticService>();

        Assert.Null(diagnostics.GetSnapshot());
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, time.TimerCount);

        await bootstrap.StopAsync();
        time.Dispose();
    }

    [Fact]
    public async Task StopAsync_DoesNotReleasePidGuardOwnedByOuterProcessScope()
    {
        using var directory = new TemporaryDirectory();
        var pidPath = Path.Combine(directory.Path, "diagnostic-test.pid");
        using var guard = PidFileGuard.Acquire(directory.Path, "diagnostic-test.pid");
        var time = new DiagnosticTimeProvider();
        var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None)
            .RegisterServices(services => RegisterCollector(
                services,
                time,
                [],
                new DiagnosticOptions { Enabled = false }
            ));

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), File.ReadAllText(pidPath));
        time.Dispose();
    }

    private static Container RegisterDiagnosticHost(
        Container services,
        DiagnosticTimeProvider time,
        DiagnosticTestPlugin plugin)
    {
        services.RegisterInstance<IPluginLoaderService>(plugin);
        services.RegisterInstance(new DiagnosticOptions());
        services.RegisterInstance<TimeProvider>(time);
        services.RegisterInstance<IGameLoopService>(new GameLoopMetricsSourceStub(new GameLoopMetricsSnapshot()));
        services.RegisterInstance<ITimerService>(new TimerMetricsSourceStub(new TimerMetricsSnapshot()));
        services.RegisterInstance<ISessionService>(new SessionCountSourceStub(0));
        services.RegisterMoongateService<IEventBusService, EventBusService>();
        services.Register<IMetricProvider, SystemMetricsProvider>(Reuse.Singleton);
        services.Register<IMetricProvider, GameLoopMetricsProvider>(Reuse.Singleton);
        services.Register<IMetricProvider, TimerMetricsProvider>(Reuse.Singleton);
        services.Register<IMetricProvider, SessionMetricsProvider>(Reuse.Singleton);
        services.RegisterMoongateService<IDiagnosticService, DiagnosticService>(DiagnosticService.StartupPriority);
        return services;
    }

    private static Container RegisterCollector(
        Container services,
        DiagnosticTimeProvider time,
        IEnumerable<IMetricProvider> providers,
        DiagnosticOptions? options = null)
    {
        services.RegisterInstance(options ?? new DiagnosticOptions());
        services.RegisterInstance<TimeProvider>(time);
        services.RegisterMoongateService<IEventBusService, EventBusService>();
        foreach (var provider in providers)
        {
            services.RegisterInstance<IMetricProvider>(provider);
        }
        return services.RegisterMoongateService<IDiagnosticService, DiagnosticService>(DiagnosticService.StartupPriority);
    }
}
