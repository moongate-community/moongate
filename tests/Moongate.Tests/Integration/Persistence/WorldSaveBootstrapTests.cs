using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Server;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class WorldSaveBootstrapTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Registration_ResolvesOneSharedWorldSaveService()
    {
        using var container = new Container();
        container.RegisterInstance(TimeProvider.System);
        container.RegisterInstance(new WorldSaveOptions());
        container.RegisterInstance(new PersistenceOperationBarrier());

        // The concrete constructor dependencies are shared by the same registration path used by the host.
        using var fixture = new TemporaryPersistenceDirectory();
        var persistence = new MoongatePersistenceService(new());
        container.RegisterInstance(persistence);
        container.RegisterInstance(new DirectoriesConfig(fixture.Path, []));
        var timers = new TimerWheelService(
            new(),
            TimeProvider.System
        );
        container.RegisterInstance<ITimerService>(timers);
        container.RegisterInstance<IGameLoopService>(
            new GameLoopService(
                new(),
                timers,
                TimeProvider.System
            )
        );
        container.RegisterMoongateService<IWorldSaveService, WorldSaveService>(WorldSaveService.StartupPriority);
        Assert.Same(container.Resolve<IWorldSaveService>(), container.Resolve<IWorldSaveService>());
    }

    [Fact]
    public async Task StartAsync_ActivationFails_RollsBackWithoutSaving()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        using var container = CreateContainer(fixture);
        container.OnEvent<MoongateStartedEvent>((_, _) => fixture.Timers.StopAsync());
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bootstrap.StartAsync().WaitAsync(Timeout));
        await bootstrap.StopAsync();
        Assert.Equal(0, fixture.Captures);
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task StartAsync_NonAutostartWorldSaveRegistration_IsNotActivatedOrStopped()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        using var container = CreateContainer(fixture, false);
        container.RegisterInstance<IWorldSaveService>(fixture.Saves);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        await bootstrap.StopAsync();
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StartAsync_StartedPublicationCanceled_NeverSavesPartialWorldAndDrainsRollback()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        using var container = CreateContainer(fixture);
        using var cancellation = new CancellationTokenSource();
        var firstRan = false;
        container.OnEvent<MoongateStartedEvent>(
            async (_, _) =>
            {
                await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "partial startup");
                firstRan = true;
            }
        );
        container.OnEvent<MoongateStartedEvent>(
            (_, _) =>
            {
                cancellation.Cancel();

                return Task.CompletedTask;
            }
        );
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bootstrap.StartAsync().WaitAsync(Timeout));
        await bootstrap.StopAsync().WaitAsync(Timeout);
        Assert.True(firstRan);
        Assert.Equal(0, fixture.Captures);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().RegisteredTimers);
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
        Assert.True(container.IsDisposed);
        Assert.Null(await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StartAsync_StartedSubscribersFinishBeforeActivationAndAutosave()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        using var container = CreateContainer(fixture);
        container.OnEvent<MoongateStartedEvent>(
            async (_, _) =>
            {
                Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
                await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "initialized by subscriber");
            }
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync().WaitAsync(Timeout);
        Assert.Equal(1, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        await fixture.Saves.SaveAsync().WaitAsync(Timeout);
        await bootstrap.StopAsync().WaitAsync(Timeout);
        Assert.Equal("initialized by subscriber", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StartAsync_ThrowingObserverStaysIsolated_ActivatesAfterRemainingObservers()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        using var container = CreateContainer(fixture);
        container.OnEvent<MoongateStartedEvent>((_, _) => throw new ApplicationException("isolated observer"));
        var lastRan = false;
        container.OnEvent<MoongateStartedEvent>(
            async (_, _) =>
            {
                Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
                await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
                await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "startup complete");
                lastRan = true;
            }
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync().WaitAsync(Timeout);
        Assert.True(lastRan);
        Assert.Equal(1, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        await bootstrap.StopAsync().WaitAsync(Timeout);
        Assert.Equal("startup complete", await fixture.ReadSavedNameAsync());
    }

    [Theory, InlineData(false, false), InlineData(true, false), InlineData(false, true), InlineData(true, true)]
    public async Task StopAsync_FatalLoopCommand_ReportsOriginalOnceAndKeepsDistinctCleanupFailure(
        bool failCleanup,
        bool aggregateLoopFailure
    )
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        using var container = CreateContainer(fixture);
        Exception failure = aggregateLoopFailure
                                ? new AggregateException(new ApplicationException("fatal command"))
                                : new ApplicationException("fatal command");
        var cleanupFailure = new IOException("owner cleanup failed");

        if (failCleanup)
        {
            container.RegisterMoongateService(
                new CallbackStartupService(() => Task.CompletedTask, () => throw cleanupFailure)
            );
        }

        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();
        await fixture.Loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Loop.Completion.WaitAsync(Timeout)));
        var observed = await Record.ExceptionAsync(() => bootstrap.StopAsync().WaitAsync(Timeout));

        if (failCleanup)
        {
            Assert.Equal([failure, cleanupFailure], Assert.IsType<AggregateException>(observed).InnerExceptions);
        }
        else
        {
            Assert.Same(failure, observed);
        }

        Assert.True(container.IsDisposed);
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StopAsync_FinalSaveCompletesBeforeWorldOwnerClearsItsLiveState()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        using var container = CreateContainer(fixture);
        var ownerStopped = false;
        container.RegisterMoongateService(
            new CallbackStartupService(
                () => Task.CompletedTask,
                async () =>
                {
                    Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
                    Assert.Equal("last queued mutation", await fixture.ReadSavedNameAsync());
                    ownerStopped = true;
                    fixture.Entities.Clear();
                }
            )
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        await bootstrap.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        await fixture.Loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        await fixture.Loop.PostAsync(new ActionGameLoopWorkItem(() => fixture.Entities[0].Name = "last queued mutation"));
        await fixture.BlockWritesAsync();
        var stopping = bootstrap.StopAsync();
        Assert.False(stopping.IsCompleted);
        Assert.False(ownerStopped);
        blocker.Release();
        await fixture.WaitForBlockedWriteAsync();
        Assert.False(ownerStopped);
        await fixture.ReleaseWritesAsync();
        await stopping.WaitAsync(Timeout);
        Assert.True(ownerStopped);
        Assert.Equal("last queued mutation", await fixture.ReadSavedNameAsync());
    }

    private static Container CreateContainer(WorldSaveFixture fixture, bool registerSave = true)
    {
        var container = new Container();
        container.RegisterInstance(fixture.Persistence, setup: Setup.With(preventDisposal: true));
        container.RegisterMoongateService<ITimerService>(fixture.Timers, -900);
        container.RegisterMoongateService<IGameLoopService>(fixture.Loop, -800);

        if (registerSave)
        {
            container.RegisterMoongateService<IWorldSaveService>(fixture.Saves, WorldSaveService.StartupPriority);
        }

        return container;
    }
}
