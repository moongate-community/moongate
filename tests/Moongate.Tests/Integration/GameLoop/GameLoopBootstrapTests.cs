using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.Support.Server;

namespace Moongate.Tests.Integration.GameLoop;

public sealed class GameLoopBootstrapTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task RunAsync_CommandFailsDuringShutdown_PreservesFailureAfterCleanup()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdown = new SignalingShutdownService();
        container.AddMoongateService(shutdown);
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        var failure = new InvalidOperationException("game command failed while draining");
        var run = MoongateServerRunner.RunAsync(bootstrap);
        await bootstrap.StartAsync();

        try
        {
            await loop.PostAsync(
                new ActionGameLoopWorkItem(
                    () =>
                    {
                        entered.SetResult();

                        if (!release.Wait(TestTimeout))
                        {
                            throw new TimeoutException("The test did not release the game command.");
                        }

                        throw failure;
                    }
                )
            );
            await entered.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            await shutdown.ShutdownStarted.WaitAsync(TestTimeout);
            release.Set();

            var actual = await Record.ExceptionAsync(() => run.WaitAsync(TestTimeout));

            Assert.Same(failure, actual);
            Assert.True(container.IsDisposed);
        }
        finally
        {
            release.Set();
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    [Theory, InlineData(false, false), InlineData(true, false), InlineData(false, true), InlineData(true, true)]
    public async Task RunAsync_ExternalStopAlreadyObservedFailure_ReportsEachFailureOnce(
        bool failOtherService,
        bool aggregateHandlerFailure
    )
    {
        using var container = CreateContainer();
        Exception loopFailure = aggregateHandlerFailure
                                    ? new AggregateException(
                                        new InvalidOperationException("game command failed before external shutdown")
                                    )
                                    : new InvalidOperationException("game command failed before external shutdown");
        var cleanupFailure = new IOException("other service failed to stop");

        if (failOtherService)
        {
            container.AddMoongateService(new RecordingStartupService("consumer", [], stopFailure: cleanupFailure));
        }

        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        var loop = container.Resolve<IGameLoopService>();
        await bootstrap.StartAsync();
        await loop.PostAsync(new ActionGameLoopWorkItem(() => throw loopFailure));
        Assert.Same(loopFailure, await Record.ExceptionAsync(() => loop.Completion.WaitAsync(TestTimeout)));
        Assert.NotNull(await Record.ExceptionAsync(() => bootstrap.StopAsync().WaitAsync(TestTimeout)));

        var actual = await Record.ExceptionAsync(() => MoongateServerRunner.RunAsync(bootstrap).WaitAsync(TestTimeout));

        if (failOtherService)
        {
            Assert.Equal([loopFailure, cleanupFailure], Assert.IsType<AggregateException>(actual).InnerExceptions);
        }
        else
        {
            Assert.Same(loopFailure, actual);
        }

        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task RunAsync_HostCanceled_DrainsAcceptedWorkBeforeDisposingContainer()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        var ran = 0;
        var run = MoongateServerRunner.RunAsync(bootstrap);
        await bootstrap.StartAsync();

        try
        {
            await loop.PostAsync(
                new ActionGameLoopWorkItem(
                    () =>
                    {
                        entered.SetResult();

                        if (!release.Wait(TestTimeout))
                        {
                            throw new TimeoutException("The test did not release the game command.");
                        }
                    }
                )
            );
            await entered.Task.WaitAsync(TestTimeout);
            await loop.PostAsync(
                new ActionGameLoopWorkItem(
                    () =>
                    {
                        Assert.False(container.IsDisposed);
                        Interlocked.Increment(ref ran);
                    }
                )
            );

            cancellation.Cancel();
            Assert.False(run.IsCompleted);
            release.Set();
            await run.WaitAsync(TestTimeout);

            Assert.Equal(1, ran);
            Assert.True(loop.Completion.IsCompletedSuccessfully);
            Assert.True(container.IsDisposed);
        }
        finally
        {
            release.Set();
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    [Fact]
    public async Task RunAsync_LoopAlreadyFaultedAndHostCanceled_PreservesLoopFailure()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        var failure = new InvalidOperationException("game command failed before host cancellation");
        await bootstrap.StartAsync();
        await loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.Completion.WaitAsync(TestTimeout));
        cancellation.Cancel();

        var actual = await Record.ExceptionAsync(() => MoongateServerRunner.RunAsync(bootstrap).WaitAsync(TestTimeout));

        Assert.Same(failure, actual);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task RunAsync_LoopAndCleanupFail_PreservesBothFailuresWithoutDuplicatingLoopFault()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        var loopFailure = new InvalidOperationException("game command failed");
        var stopFailure = new IOException("another service failed to stop");
        container.AddMoongateService(new RecordingStartupService("consumer", [], stopFailure: stopFailure));
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        await bootstrap.StartAsync();
        var run = MoongateServerRunner.RunAsync(bootstrap);

        try
        {
            await loop.PostAsync(new ActionGameLoopWorkItem(() => throw loopFailure));
            var actual = await Assert.ThrowsAsync<AggregateException>(() => run.WaitAsync(TestTimeout));

            Assert.Equal([loopFailure, stopFailure], actual.InnerExceptions);
            Assert.True(container.IsDisposed);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    [Fact]
    public async Task RunAsync_LoopStoppedExternally_ExitsAndDisposesHost()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        await bootstrap.StartAsync();
        var run = MoongateServerRunner.RunAsync(bootstrap);

        try
        {
            await loop.StopAsync();
            await run.WaitAsync(TestTimeout);

            Assert.True(container.IsDisposed);
            Assert.True(loop.Completion.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    [Fact]
    public async Task RunAsync_WorkItemFault_PropagatesOriginalFailureAndDisposesHost()
    {
        using var container = CreateContainer();
        using var cancellation = new CancellationTokenSource();
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);
        var loop = container.Resolve<IGameLoopService>();
        var failure = new InvalidOperationException("game command failed");
        await bootstrap.StartAsync();
        var run = MoongateServerRunner.RunAsync(bootstrap);

        try
        {
            await loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));

            var actual = await Record.ExceptionAsync(() => run.WaitAsync(TestTimeout));

            Assert.Same(failure, actual);
            Assert.True(container.IsDisposed);
            Assert.True(loop.Completion.IsFaulted);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TestTimeout)
                     .ConfigureAwait(
                         ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext
                     );
        }
    }

    [Fact]
    public async Task StartAsync_LaterServiceFails_PreservesLoopFailureDuringRollback()
    {
        using var container = CreateContainer();
        using var blocker = new BlockingGameLoopWorkItem();
        var loop = container.Resolve<IGameLoopService>();
        var startupFailure = new IOException("later service failed to start");
        var loopFailure = new InvalidOperationException("game command failed during rollback");
        container.AddMoongateService(
            new CallbackStartupService(
                async () =>
                {
                    await loop.PostAsync(
                        new ActionGameLoopWorkItem(
                            () =>
                            {
                                blocker.Execute();

                                throw loopFailure;
                            }
                        )
                    );
                    await blocker.Entered.WaitAsync(TestTimeout);

                    throw startupFailure;
                },
                () =>
                {
                    blocker.Release();

                    return Task.CompletedTask;
                }
            )
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var actual = await Assert.ThrowsAsync<AggregateException>(
                         () =>
                             MoongateServerRunner.RunAsync(bootstrap).WaitAsync(TestTimeout)
                     );

        Assert.Equal([startupFailure, loopFailure], actual.InnerExceptions);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task StartAsync_ServiceObservesLoopFault_ReportsOriginalFailureOnce()
    {
        using var container = CreateContainer();
        var loop = container.Resolve<IGameLoopService>();
        var failure = new InvalidOperationException("game command failed during startup");
        container.AddMoongateService(
            new CallbackStartupService(
                async () =>
                {
                    await loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
                    await loop.Completion.WaitAsync(TestTimeout);
                },
                () => Task.CompletedTask
            )
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        var actual = await Record.ExceptionAsync(() => MoongateServerRunner.RunAsync(bootstrap).WaitAsync(TestTimeout));

        Assert.Same(failure, actual);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task StopAsync_LoopFaultWithoutRun_ReportsFailureAndDisposesHost()
    {
        using var container = CreateContainer();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        var loop = container.Resolve<IGameLoopService>();
        var failure = new InvalidOperationException("game command failed without a run waiter");
        await bootstrap.StartAsync();
        await loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        await Assert.ThrowsAsync<InvalidOperationException>(() => loop.Completion.WaitAsync(TestTimeout));

        var actual = await Record.ExceptionAsync(() => bootstrap.StopAsync().WaitAsync(TestTimeout));

        Assert.Same(failure, actual);
        Assert.True(container.IsDisposed);
    }

    private static Container CreateContainer()
    {
        var container = new Container();
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterInstance(new TimerWheelOptions());
        container.AddMoongateService<TimerWheelService>(-900);
        container.RegisterDelegate<ITimerService>(services => services.Resolve<TimerWheelService>(), Reuse.Singleton);
        container.RegisterInstance(new GameLoopOptions { QueueCapacity = 4, MaxWorkItemsPerBatch = 2 });
        container.AddMoongateService<IGameLoopService, GameLoopService>(-800);

        return container;
    }
}
