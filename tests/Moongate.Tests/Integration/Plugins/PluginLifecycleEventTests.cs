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
using Moongate.Tests.TestSupport.Persistence;
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
                    }
                );
                container.OnEvent<MoongateStoppingEvent>((_, _) =>
                    {
                        events.Add("stopping");
                        return Task.CompletedTask;
                    }
                );
                container.OnEvent<MoongateStoppedEvent>((_, _) =>
                    {
                        events.Add("stopped");
                        return Task.CompletedTask;
                    }
                );
            }
        );
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
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        var events = new List<string>();
        var serial = new Serial(0x40000001);
        var service = new RecordingStartupService("plugin", events);
        var container = fixture.Container;
        fixture.RegisterEntity();
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        var plugin = CreatePlugin(pluginContainer =>
            {
                events.Add("plugin:register");
                pluginContainer.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
                pluginContainer.OnEvent<MoongateStartedEvent>(async (_, _) =>
                    {
                        Assert.Empty(
                            await pluginContainer.Resolve<IDataAccess<TestEntity>>().GetAllAsync(CancellationToken.None)
                        );
                        events.Add("started");
                    }
                );
                pluginContainer.OnEvent<MoongateStoppingEvent>(async (_, token) =>
                    {
                        events.Add("stopping");
                        await pluginContainer.Resolve<IDataAccess<TestEntity>>()
                            .UpsertAsync(
                                new TestEntity { Id = serial, Name = "saved while stopping" },
                                token
                            );
                    }
                );
                pluginContainer.OnEvent<MoongateStoppedEvent>(async (_, token) =>
                    {
                        Assert.NotNull(pluginContainer.Resolve<RecordingDisposable>());
                        Assert.Equal(
                            "saved while stopping",
                            await fixture.Database.ScalarAsync<string>("SELECT name FROM host_test.items")
                        );
                        events.Add("stopped");
                    }
                );
            }
        );
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
                    }
                );
                pluginContainer.OnEvent<MoongateStoppingEvent>((_, _) =>
                    {
                        events.Add("stopping");
                        return Task.CompletedTask;
                    }
                );
                pluginContainer.OnEvent<MoongateStoppedEvent>((_, _) =>
                    {
                        events.Add("stopped");
                        return Task.CompletedTask;
                    }
                );
            }
        );
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
                }
            )
            .OnEvent<MoongateStoppedEvent>((_, _) =>
                {
                    events.Add("stopped");
                    return Task.CompletedTask;
                }
            );
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
            }
        );
        container.OnEvent<MoongateStoppedEvent>(async (_, token) =>
            {
                Assert.False(token.IsCancellationRequested);
                Assert.False(token.CanBeCanceled);
                await Task.Delay(1, token);
                events.Add("stopped");
            }
        );
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
                }
            )
            .OnEvent<MoongateStoppingEvent>((_, _) =>
                {
                    events.Add("stopping");
                    return Task.CompletedTask;
                }
            );
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
                }
            )
            .OnEvent<MoongateStoppedEvent>((_, _) =>
                {
                    events.Add("stopped");
                    return Task.CompletedTask;
                }
            );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var start = bootstrap.StartAsync();
        await startEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = bootstrap.StopAsync();
        releaseStart.SetResult();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => start.WaitAsync(TimeSpan.FromSeconds(5))
        );
        await stop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(startFailure, failure);
        Assert.Equal(["start:enter", "stopping", "stop:service", "stopped"], events);
    }

    [Fact]
    public async Task StopAsync_StartedCallbackRequestsStop_WaitsForCallbackBeforeShutdown()
    {
        var events = new List<string>();
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var container = new Container();
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        Task? requestedStop = null;
        container.RegisterMoongatePlugin(
            CreatePlugin(pluginContainer => pluginContainer
                .OnEvent<MoongateStartedEvent>(async (_, _) =>
                    {
                        events.Add("started:enter");
                        requestedStop = bootstrap.StopAsync();
                        await releaseObserver.Task;
                        events.Add("started:exit");
                    }
                )
                .OnEvent<MoongateStoppingEvent>((_, _) =>
                    {
                        events.Add("stopping");
                        return Task.CompletedTask;
                    }
                )
                .OnEvent<MoongateStoppedEvent>((_, _) =>
                    {
                        events.Add("stopped");
                        return Task.CompletedTask;
                    }
                )
            )
        );

        var start = bootstrap.StartAsync();
        var eventsWhileStarted = events.ToArray();
        var stopCompletedWhileStarted = requestedStop?.IsCompleted;
        releaseObserver.SetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(requestedStop);
        await requestedStop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(stopCompletedWhileStarted);
        Assert.Equal(["started:enter"], eventsWhileStarted);
        Assert.Same(requestedStop, bootstrap.StopAsync());
        Assert.Equal(["started:enter", "started:exit", "stopping", "stopped", "container:dispose"], events);
    }

    [Fact]
    public async Task StopAsync_StoppingCallbackRequestsStop_ReturnsSameTaskAndPublishesOnce()
    {
        var events = new List<string>();
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var container = new Container();
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        Task? requestedStop = null;
        var stoppingCount = 0;
        container.OnEvent<MoongateStoppingEvent>(async (_, _) =>
                {
                    events.Add("stopping:enter");
                    if (++stoppingCount == 1)
                    {
                        requestedStop = bootstrap.StopAsync();
                        await releaseObserver.Task;
                    }

                    events.Add("stopping:exit");
                }
            )
            .OnEvent<MoongateStoppedEvent>((_, _) =>
                {
                    events.Add("stopped");
                    return Task.CompletedTask;
                }
            );
        await bootstrap.StartAsync();

        var stop = bootstrap.StopAsync();
        var eventsWhileStopping = events.ToArray();
        releaseObserver.SetResult();
        var failure = await Record.ExceptionAsync(() => stop.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(requestedStop);
        await requestedStop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(failure);
        Assert.Same(stop, requestedStop);
        Assert.Equal(["stopping:enter"], eventsWhileStopping);
        Assert.Equal(["stopping:enter", "stopping:exit", "stopped", "container:dispose"], events);
    }

    [Fact]
    public async Task StartAsync_FailedStartStoppingCallbackRequestsStop_SharesCleanupWithoutDeadlock()
    {
        var events = new List<string>();
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startFailure = new InvalidOperationException("start failed");
        var service = new RecordingStartupService("service", events, startFailure: startFailure);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        Task? requestedStop = null;
        var stoppingCount = 0;
        container.OnEvent<MoongateStartedEvent>((_, _) =>
                {
                    events.Add("started");
                    return Task.CompletedTask;
                }
            )
            .OnEvent<MoongateStoppingEvent>(async (_, _) =>
                {
                    events.Add("stopping:enter");
                    if (++stoppingCount == 1)
                    {
                        requestedStop = bootstrap.StopAsync();
                        await releaseObserver.Task;
                    }

                    events.Add("stopping:exit");
                }
            )
            .OnEvent<MoongateStoppedEvent>((_, _) =>
                {
                    events.Add("stopped");
                    return Task.CompletedTask;
                }
            );

        var start = bootstrap.StartAsync();
        var eventsWhileStopping = events.ToArray();
        var stopCompletedWhileStopping = requestedStop?.IsCompleted;
        releaseObserver.SetResult();
        var failure = await Record.ExceptionAsync(() => start.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(requestedStop);
        await requestedStop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(startFailure, failure);
        Assert.False(stopCompletedWhileStopping);
        Assert.Same(requestedStop, bootstrap.StopAsync());
        Assert.Equal(["start:service", "stopping:enter"], eventsWhileStopping);
        Assert.Equal(
            ["start:service", "stopping:enter", "stopping:exit", "stop:service", "stopped", "container:dispose"],
            events
        );
    }

    [Fact]
    public async Task StartAsync_StartedCallbackRequestsStart_ReturnsSameTaskAndPublishesOnce()
    {
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        Task? requestedStart = null;
        var startedCount = 0;
        container.OnEvent<MoongateStartedEvent>(async (_, _) =>
            {
                if (++startedCount == 1)
                {
                    requestedStart = bootstrap.StartAsync();
                    await releaseObserver.Task;
                }
            }
        );

        var start = bootstrap.StartAsync();
        var requestedStartCompletedInCallback = requestedStart?.IsCompleted;
        releaseObserver.SetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(requestedStart);
        await requestedStart.WaitAsync(TimeSpan.FromSeconds(5));
        await bootstrap.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(start, requestedStart);
        Assert.False(requestedStartCompletedInCallback);
        Assert.Equal(1, startedCount);
    }

    private static RecordingPlugin CreatePlugin(Action<Container> register)
    {
        return new RecordingPlugin(
            new MoongatePluginData(
                "com.github.moongate.tests.lifecycle",
                "Lifecycle test",
                new Version(1, 0, 0)
            ),
            register
        );
    }
}
