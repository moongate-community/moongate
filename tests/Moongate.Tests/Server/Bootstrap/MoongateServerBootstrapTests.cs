using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Services.Persistence.Internal;
using Moongate.Tests.Support.Events;
using Moongate.Tests.Support.Persistence;
using Moongate.Tests.Support.Server;
using Moongate.Tests.Support.Server.Interfaces;

namespace Moongate.Tests.Server.Bootstrap;

public class MoongateServerBootstrapTests
{
    [Fact]
    public async Task RegisterServices_ConfiguresExistingContainerBeforeStartingServices()
    {
        using var container = new Container();
        var events = new List<string>();
        var calls = 0;
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var result = bootstrap.RegisterServices(services =>
        {
            Assert.Same(container, services);
            calls++;
            events.Add("register");
            return services.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(() =>
            {
                events.Add("construct");
                return new RecordingStartupService("custom", events);
            });
        });

        Assert.Same(bootstrap, result);
        Assert.Equal(["register"], events);
        await bootstrap.StartAsync();
        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(1, calls);
        Assert.Equal(["register", "construct", "start:custom", "stop:custom"], events);
    }

    [Fact]
    public async Task RegisterServices_CustomEventBus_ReceivesLifecycleEvents()
    {
        using var container = new Container();
        using var busContainer = new Container();
        busContainer.RegisterMoongateEventBus();
        var eventBus = busContainer.Resolve<IMoongateEventBus>();
        var events = new List<string>();
        eventBus.Subscribe<MoongateStartedEvent>((_, _) =>
        {
            events.Add("started");
            return Task.CompletedTask;
        });
        eventBus.Subscribe<MoongateStoppedEvent>((_, _) =>
        {
            events.Add("stopped");
            return Task.CompletedTask;
        });
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        bootstrap.RegisterServices(services =>
        {
            services.RegisterInstance(eventBus, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
            return services;
        });

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(["started", "stopped"], events);
    }

    [Fact]
    public async Task RegisterServices_InvalidCallbackOrReturnedContainer_IsRejected()
    {
        using var container = new Container();
        using var other = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        Assert.Throws<ArgumentNullException>(() => bootstrap.RegisterServices(null!));
        Assert.Throws<InvalidOperationException>(() => bootstrap.RegisterServices(_ => null!));
        Assert.Throws<InvalidOperationException>(() => bootstrap.RegisterServices(_ => other));

        await bootstrap.StopAsync();
        Assert.True(container.IsDisposed);
        Assert.False(other.IsDisposed);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task RegisterServices_AfterLifecycleBegins_DoesNotInvokeCallback(bool startFirst)
    {
        using var container = new Container();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        if (startFirst)
        {
            await bootstrap.StartAsync();
        }
        else
        {
            await bootstrap.StopAsync();
        }

        var invoked = false;
        Assert.Throws<InvalidOperationException>(() => bootstrap.RegisterServices(services =>
        {
            invoked = true;
            return services;
        }));
        Assert.False(invoked);
        await bootstrap.StopAsync();
    }

    [Theory, InlineData("register"), InlineData("start"), InlineData("stop")]
    public async Task RegisterServices_ReentrantLifecycle_IsRejectedWithoutStartingServices(string operation)
    {
        using var container = new Container();
        var events = new List<string>();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        bootstrap.RegisterServices(services =>
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case "register": bootstrap.RegisterServices(value => value); break;
                    case "start": bootstrap.StartAsync().GetAwaiter().GetResult(); break;
                    case "stop": bootstrap.StopAsync().GetAwaiter().GetResult(); break;
                }
            });
            return services.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(
                new RecordingStartupService("custom", events));
        });

        Assert.Empty(events);
        await bootstrap.StartAsync();
        await bootstrap.StopAsync();
        Assert.Equal(["start:custom", "stop:custom"], events);
    }

    [Fact]
    public async Task RegisterServices_CallbackThrows_PropagatesFailureAndAllowsCleanup()
    {
        using var container = new Container();
        var events = new List<string>();
        var failure = new InvalidOperationException("registration failed");
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var actual = Assert.Throws<InvalidOperationException>(() => bootstrap.RegisterServices(services =>
        {
            services.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(
                new RecordingStartupService("custom", events));
            throw failure;
        }));

        Assert.Same(failure, actual);
        await bootstrap.StopAsync();
        Assert.Empty(events);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task Constructor_NoPluginsOrServices_EnsuresSharedEventBus()
    {
        var container = new Container();
        Assert.False(container.IsRegistered<IMoongateEventBus>());

        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        Assert.True(container.IsRegistered<IMoongateEventBus>());
        Assert.Same(container.Resolve<IMoongateEventBus>(), container.Resolve<IMoongateEventBus>());
        await bootstrap.StartAsync();
        await bootstrap.StopAsync();
    }

    [Fact]
    public async Task StartAndStopAsync_AutostartMetadata_OrdersServicesAndDeduplicatesAliases()
    {
        var events = new List<string>();
        var early = new RecordingStartupService("early", events);
        var late = new RecordingStartupService("late", events);
        var resolvedNonAutostart = false;
        var container = new Container();
        container.RegisterMoongateService<object>(() =>
        {
            resolvedNonAutostart = true;

            return new object();
        }, -20);
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(early, -10);
        container.RegisterMoongateService<RecordingStartupService>(early, 5);
        container.RegisterMoongateService<ISecondaryRecordingStartupService, RecordingStartupService>(late, 10);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.False(resolvedNonAutostart);
        Assert.Equal(["start:early", "start:late", "stop:late", "stop:early"], events);
    }

    [Fact]
    public async Task StartAsync_LaterConstructorReadsPersistence_ResolvesAfterInitialization()
    {
        using var root = new TemporaryPersistenceDirectory();
        var events = new List<string>();
        var constructorReadInitializedPersistence = false;
        var container = new Container();
        container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items")
            .RegisterMoongateService<MoongatePersistenceStartupService>(MoongatePersistenceStartupService.StartupPriority)
            .RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(resolver =>
            {
                Assert.Empty(resolver.Resolve<IDataAccess<TestEntity>>().GetAll());
                constructorReadInitializedPersistence = true;

                return new RecordingStartupService("consumer", events);
            });
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.True(constructorReadInitializedPersistence);
        Assert.Equal(["start:consumer", "stop:consumer"], events);
    }

    [Fact]
    public async Task StartAsync_LaterStartFails_StopsFailingAndStartedServicesAndReleasesPersistence()
    {
        using var root = new TemporaryPersistenceDirectory();
        var events = new List<string>();
        var startFailure = new InvalidOperationException("start failed");
        var failing = new RecordingStartupService("failing", events, startFailure: startFailure);
        var container = new Container();
        container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items")
            .RegisterMoongateService<MoongatePersistenceStartupService>(MoongatePersistenceStartupService.StartupPriority)
            .RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(failing);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.StartAsync());

        Assert.Same(startFailure, failure);
        Assert.Equal(["start:failing", "stop:failing"], events);
        await using (var reopened = new MoongatePersistenceService(root.Path))
        {
            reopened.Register<TestEntity>("items");
            await reopened.InitializeAsync();
        }
        await bootstrap.StopAsync();
    }

    [Fact]
    public async Task StartAsync_StartAndCleanupFail_PreservesBothFailures()
    {
        var events = new List<string>();
        var cleanupFailure = new IOException("cleanup failed");
        var startFailure = new InvalidOperationException("start failed");
        var early = new RecordingStartupService("early", events, stopFailure: cleanupFailure);
        var failing = new RecordingStartupService("failing", events, startFailure: startFailure);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(early, -1)
            .RegisterMoongateService<ISecondaryRecordingStartupService, RecordingStartupService>(failing, 0);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var failure = await Assert.ThrowsAsync<AggregateException>(() => bootstrap.StartAsync());

        Assert.Equal([startFailure, cleanupFailure], failure.InnerExceptions);
        Assert.Equal(["start:early", "start:failing", "stop:failing", "stop:early"], events);
        await bootstrap.StopAsync();
    }

    [Fact]
    public async Task StartAsync_MetadataPreparationAndCleanupFail_PreservesFailuresAndCleansUpOnce()
    {
        var events = new List<string>();
        var metadataFailure = new InvalidOperationException("metadata failed");
        var stoppingFailure = new IOException("stopping failed");
        var stoppedFailure = new ApplicationException("stopped failed");
        var eventBus = new FaultingLifecycleEventBus(events, stoppingFailure, stoppedFailure);
        var container = new Container();
        container.RegisterDelegate<List<ServiceRegistrationData>>(
            () => throw metadataFailure,
            Reuse.Singleton
        );
        container.RegisterInstance<IMoongateEventBus>(eventBus);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var failure = await Assert.ThrowsAsync<AggregateException>(() => bootstrap.StartAsync());

        Assert.Equal([metadataFailure, stoppingFailure, stoppedFailure], failure.InnerExceptions);
        Assert.Equal(["stopping", "stopped"], events);

        await bootstrap.StopAsync();

        Assert.Equal(["stopping", "stopped"], events);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task StartAsync_ServiceCancellation_PreservesCancellationAndCleanupFailures(bool cleanupFails)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var startFailure = new OperationCanceledException(cancellation.Token);
        var cleanupFailure = new IOException("cleanup failed");
        var events = new List<string>();
        var service = new RecordingStartupService(
            "service", events, startFailure: startFailure, stopFailure: cleanupFails ? cleanupFailure : null
        );
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);

        var start = bootstrap.StartAsync();
        var failure = await Record.ExceptionAsync(() => start.WaitAsync(TimeSpan.FromSeconds(5)));
        await bootstrap.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

        if (cleanupFails)
        {
            var aggregate = Assert.IsType<AggregateException>(failure);
            Assert.Equal([startFailure, cleanupFailure], aggregate.InnerExceptions);
            Assert.True(start.IsFaulted);
        }
        else
        {
            Assert.Same(startFailure, failure);
            Assert.True(start.IsCanceled);
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        Assert.Same(start, bootstrap.StartAsync());
        Assert.Equal(["start:service", "stop:service"], events);
    }

    [Fact]
    public async Task StopAsync_ServiceCancellation_PreservesOriginalCancellationInSharedTask()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var stopFailure = new OperationCanceledException(cancellation.Token);
        var events = new List<string>();
        var service = new RecordingStartupService("service", events, stopFailure: stopFailure);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();

        var stop = bootstrap.StopAsync();
        var failure = await Record.ExceptionAsync(() => stop.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(stopFailure, failure);
        Assert.True(stop.IsCanceled);
        Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(stop, bootstrap.StopAsync());
        Assert.Equal(["start:service", "stop:service"], events);
    }

    [Fact]
    public async Task StopAsync_StopFails_StopsEveryServiceReleasesPersistenceAndDoesNotRepeatWork()
    {
        using var root = new TemporaryPersistenceDirectory();
        var events = new List<string>();
        var stopFailure = new IOException("stop failed");
        var failing = new RecordingStartupService("failing", events, stopFailure: stopFailure);
        var container = new Container();
        container.RegisterMoongatePersistence(root.Path)
            .RegisterDataAccess<TestEntity>("items")
            .RegisterMoongateService<MoongatePersistenceStartupService>(MoongatePersistenceStartupService.StartupPriority)
            .RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(failing);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();

        var firstFailure = await Assert.ThrowsAsync<IOException>(() => bootstrap.StopAsync());
        var secondFailure = await Assert.ThrowsAsync<IOException>(() => bootstrap.StopAsync());

        Assert.Same(stopFailure, firstFailure);
        Assert.Same(firstFailure, secondFailure);
        Assert.Equal(["start:failing", "stop:failing"], events);
        await using var reopened = new MoongatePersistenceService(root.Path);
        reopened.Register<TestEntity>("items");
        await reopened.InitializeAsync();
    }

    [Fact]
    public async Task RunAsync_CancellationAfterMainRunBegins_StopsStartedServices()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new List<string>();
        var service = new RecordingStartupService("service", events);
        var container = new Container();
        container.RegisterMoongateService<IRecordingStartupService, RecordingStartupService>(service);
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);

        var runTask = MoongateServerRunner.RunAsync(bootstrap);

        Assert.False(runTask.IsCompleted);
        Assert.Equal(["start:service"], events);

        cancellation.Cancel();
        await runTask;

        Assert.Equal(["start:service", "stop:service"], events);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task RunAsync_PrimaryAndShutdownFail_PreservesBothFailures(bool failDuringStart)
    {
        var events = new List<string>();
        var primaryFailure = new InvalidOperationException("primary failed");
        var shutdownFailure = new IOException("shutdown failed");
        var bootstrap = new RecordingServerBootstrap(
            events,
            startFailure: failDuringStart ? primaryFailure : null,
            runFailure: failDuringStart ? null : primaryFailure,
            stopFailure: shutdownFailure
        );

        var failure = await Assert.ThrowsAsync<AggregateException>(() => MoongateServerRunner.RunAsync(bootstrap));

        Assert.Equal([primaryFailure, shutdownFailure], failure.InnerExceptions);
        Assert.Equal(failDuringStart ? ["start", "stop"] : ["start", "run", "stop"], events);
    }
}
