using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Services.Persistence.Internal;
using Moongate.Tests.Support.Events;
using Moongate.Tests.Support.Persistence;
using Moongate.Tests.Support.Server;
using Moongate.Tests.Support.Server.Interfaces;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Plugins;

public sealed class PluginLifecycleEventTests
{
    [Fact]
    public async Task StartAndStopAsync_EventOnlyPlugin_ReceivesLifecycleWithoutServiceMetadata()
    {
        var events = new List<string>();
        var container = new Container();
        var plugin = CreatePlugin(container =>
        {
            events.Add("plugin:register");
            container.OnEvent<MoongateStartedEvent>((_, _) =>
            {
                events.Add("started");
                return Task.CompletedTask;
            });
            container.OnEvent<MoongateStoppingEvent>((_, _) =>
            {
                events.Add("stopping");
                return Task.CompletedTask;
            });
            container.OnEvent<MoongateStoppedEvent>((_, _) =>
            {
                events.Add("stopped");
                return Task.CompletedTask;
            });
        });
        container.RegisterMoongatePlugin(plugin);
        Assert.False(container.IsRegistered<List<ServiceRegistrationData>>());
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(["plugin:register", "started", "stopping", "stopped"], events);
    }

    [Fact]
    public async Task StartAndStopAsync_PluginCallbacks_ObservePersistenceAndContainerPhaseOrder()
    {
        using var root = new TemporaryPersistenceDirectory();
        var events = new List<string>();
        var serial = new Serial(0x40000001);
        var service = new RecordingStartupService("plugin", events);
        var container = new Container();
        container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items")
            .RegisterMoongateService<MoongatePersistenceStartupService>(MoongatePersistenceStartupService.StartupPriority);
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        var plugin = CreatePlugin(pluginContainer =>
        {
            events.Add("plugin:register");
            pluginContainer.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
            pluginContainer.OnEvent<MoongateStartedEvent>((_, _) =>
            {
                Assert.Empty(pluginContainer.Resolve<IDataAccess<TestEntity>>().GetAll());
                events.Add("started");
                return Task.CompletedTask;
            });
            pluginContainer.OnEvent<MoongateStoppingEvent>(async (_, token) =>
            {
                events.Add("stopping");
                await pluginContainer.Resolve<IDataAccess<TestEntity>>().UpsertAsync(
                    new TestEntity { Id = serial, Name = "saved while stopping" }, token);
            });
            pluginContainer.OnEvent<MoongateStoppedEvent>(async (_, token) =>
            {
                Assert.NotNull(pluginContainer.Resolve<RecordingDisposable>());
                await using var reopened = new MoongatePersistenceService(root.Path);
                var items = reopened.Register<TestEntity>("items");
                await reopened.InitializeAsync(token);
                Assert.Equal("saved while stopping", items.GetById(serial)?.Name);
                events.Add("stopped");
            });
        });
        container.RegisterMoongatePlugin(plugin);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(
            ["plugin:register", "start:plugin", "started", "stopping", "stop:plugin", "stopped", "container:dispose"],
            events
        );
    }

    [Fact]
    public async Task StartAsync_ServiceFails_PublishesShutdownOnceWithoutStarted()
    {
        var events = new List<string>();
        var startFailure = new InvalidOperationException("start failed");
        var service = new RecordingStartupService("failing", events, startFailure: startFailure);
        var container = new Container();
        var plugin = CreatePlugin(pluginContainer =>
        {
            events.Add("plugin:register");
            pluginContainer.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
            pluginContainer.OnEvent<MoongateStartedEvent>((_, _) =>
            {
                events.Add("started");
                return Task.CompletedTask;
            });
            pluginContainer.OnEvent<MoongateStoppingEvent>((_, _) =>
            {
                events.Add("stopping");
                return Task.CompletedTask;
            });
            pluginContainer.OnEvent<MoongateStoppedEvent>((_, _) =>
            {
                events.Add("stopped");
                return Task.CompletedTask;
            });
        });
        container.RegisterMoongatePlugin(plugin);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.StartAsync());
        await bootstrap.StopAsync();

        Assert.Same(startFailure, failure);
        Assert.Equal(
            ["plugin:register", "start:failing", "stopping", "stop:failing", "stopped"],
            events
        );
    }

    [Fact]
    public async Task StopAsync_ServiceStopFails_PublishesStoppedAfterEveryStopAttempt()
    {
        var events = new List<string>();
        var stopFailure = new IOException("stop failed");
        var early = new RecordingStartupService("early", events);
        var late = new RecordingStartupService("late", events, stopFailure: stopFailure);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(early, -1)
            .RegisterMoongateService<ISecondaryRecordingStartupService, RecordingStartupService>(late, 1)
            .OnEvent<MoongateStoppingEvent>((_, _) =>
            {
                events.Add("stopping");
                return Task.CompletedTask;
            })
            .OnEvent<MoongateStoppedEvent>((_, _) =>
            {
                events.Add("stopped");
                return Task.CompletedTask;
            });
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();

        var failure = await Assert.ThrowsAsync<IOException>(() => bootstrap.StopAsync());

        Assert.Same(stopFailure, failure);
        Assert.Equal(
            ["start:early", "start:late", "stopping", "stop:late", "stop:early", "stopped"],
            events
        );
    }

    [Fact]
    public async Task RunAsync_RunCancellation_ShutdownObserversReceiveUsableTokenAndComplete()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new List<string>();
        var container = new Container();
        container.OnEvent<MoongateStoppingEvent>(async (_, token) =>
        {
            Assert.False(token.IsCancellationRequested);
            Assert.False(token.CanBeCanceled);
            await Task.Delay(1, token);
            events.Add("stopping");
        });
        container.OnEvent<MoongateStoppedEvent>(async (_, token) =>
        {
            Assert.False(token.IsCancellationRequested);
            Assert.False(token.CanBeCanceled);
            await Task.Delay(1, token);
            events.Add("stopped");
        });
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);

        var run = MoongateServerRunner.RunAsync(bootstrap);
        await Task.Yield();
        cancellation.Cancel();
        await run;

        Assert.Equal(["stopping", "stopped"], events);
    }

    [Fact]
    public async Task StopAsync_StartedObserverInFlight_WaitsBeforeStoppingServices()
    {
        var events = new List<string>();
        var observerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new RecordingStartupService("service", events);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service)
            .OnEvent<MoongateStartedEvent>(async (_, _) =>
            {
                events.Add("started:enter");
                observerEntered.SetResult();
                await releaseObserver.Task;
                events.Add("started:exit");
            })
            .OnEvent<MoongateStoppingEvent>((_, _) =>
            {
                events.Add("stopping");
                return Task.CompletedTask;
            });
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var start = bootstrap.StartAsync();
        await observerEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = bootstrap.StopAsync();
        await Task.Yield();

        Assert.False(stop.IsCompleted);
        Assert.Equal(["start:service", "started:enter"], events);

        releaseObserver.SetResult();
        await start;
        await stop;

        Assert.Equal(
            ["start:service", "started:enter", "started:exit", "stopping", "stop:service"],
            events
        );
    }

    [Fact]
    public async Task StopAsync_StartupFailsWhileStopWaits_SharesCleanupWithoutDeadlock()
    {
        var events = new List<string>();
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startFailure = new InvalidOperationException("start failed");
        var service = new DelegateStartupService(
            async () =>
            {
                events.Add("start:enter");
                startEntered.SetResult();
                await releaseStart.Task;
                throw startFailure;
            },
            () =>
            {
                events.Add("stop:service");
                return Task.CompletedTask;
            }
        );
        var container = new Container();
        container.RegisterMoongateService(service)
            .OnEvent<MoongateStoppingEvent>((_, _) =>
            {
                events.Add("stopping");
                return Task.CompletedTask;
            })
            .OnEvent<MoongateStoppedEvent>((_, _) =>
            {
                events.Add("stopped");
                return Task.CompletedTask;
            });
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var start = bootstrap.StartAsync();
        await startEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = bootstrap.StopAsync();
        releaseStart.SetResult();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => start.WaitAsync(TimeSpan.FromSeconds(5))
        );
        await stop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(startFailure, failure);
        Assert.Equal(["start:enter", "stopping", "stop:service", "stopped"], events);
    }

    private static RecordingPlugin CreatePlugin(Action<Container> register)
    {
        return new RecordingPlugin(
            new MoongatePluginData(
                "com.github.moongate.tests.lifecycle", "Lifecycle test", new Version(1, 0, 0)
            ),
            register
        );
    }
}
