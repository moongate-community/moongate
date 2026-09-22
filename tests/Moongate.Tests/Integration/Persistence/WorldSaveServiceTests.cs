using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class WorldSaveServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Autosave_DueCallback_StartsSupervisedWorkerAndLeavesLoopResponsive()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        await fixture.StartAsync();
        await fixture.BlockWritesAsync();
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        await fixture.OnLoopAsync(() => { });
        await fixture.WaitForBlockedWriteAsync();
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "still responsive");
        var joined = fixture.Saves.SaveAsync();
        await fixture.ReleaseWritesAsync();
        await joined.WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task Autosave_OverlappingTicks_ShareOneCaptureAndStopReportsDurableFailure()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        await fixture.StartAsync();
        await fixture.Database.ExecuteAsync(
            "ALTER TABLE host_test.items ADD CONSTRAINT reject_save CHECK (name <> 'before')"
        );
        await fixture.BlockWritesAsync();
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.OnLoopAsync(() => { });
        Assert.Equal(0, fixture.Captures);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.OnLoopAsync(() => { });
        await fixture.WaitForBlockedWriteAsync();
        fixture.Clock.Advance(TimeSpan.FromSeconds(20));
        await fixture.OnLoopAsync(() => { });
        await fixture.OnLoopAsync(() => { });
        var joined = fixture.Saves.SaveAsync();
        await fixture.ReleaseWritesAsync();
        var failure = await Record.ExceptionAsync(() => joined.WaitAsync(Timeout));
        Assert.NotNull(failure);
        Assert.Equal(1, fixture.Captures);
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.StopAsync().WaitAsync(Timeout)));
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SaveAsync_AdmittedCaptureAbandonedByLoopFault_CompletesWithOriginalFailure()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        var failure = new ApplicationException("fault before queued capture");
        await fixture.Loop.PostAsync(blocker);
        await blocker.Entered.WaitAsync(Timeout);
        await fixture.Loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        var saving = fixture.Saves.SaveAsync();

        try
        {
            Assert.True(SpinWait.SpinUntil(() => fixture.Loop.GetMetricsSnapshot().QueueDepth == 2, Timeout));
        }
        finally
        {
            blocker.Release();
        }

        Assert.Same(failure, await Record.ExceptionAsync(() => saving.WaitAsync(Timeout)));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task SaveAsync_BeforeActivation_RejectsAndDoesNotScheduleAutosave()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        await fixture.StartAsync(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Equal(0, fixture.Captures);
        fixture.Saves.Activate();
        await fixture.Saves.SaveAsync().WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
    }

    [Fact]
    public async Task SaveAsync_CaptureFailure_PropagatesWithoutKillingLoop_AndStopStillClosesLoop()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        var failure = new ApplicationException("bad live source");
        fixture.CaptureFailure = failure;
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.SaveAsync().WaitAsync(Timeout)));
        await fixture.OnLoopAsync(() => { });
        var stopped = await Record.ExceptionAsync(() => fixture.Saves.StopAsync(true).WaitAsync(Timeout));
        Assert.NotNull(stopped);
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWaitersJoinOneCapture_CallerCancellationOnlyCancelsWait()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.BlockWritesAsync();
        using var cancellation = new CancellationTokenSource();
        var first = fixture.Saves.SaveAsync(cancellation.Token);
        await fixture.WaitForBlockedWriteAsync();
        var second = fixture.Saves.SaveAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
        Assert.False(second.IsCompleted);
        await fixture.ReleaseWritesAsync();
        await second.WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_DatabaseWriteBlocked_KeepsDetachedSnapshotAndAllowsLoopWork()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.BlockWritesAsync();
        var saving = fixture.Saves.SaveAsync();
        await fixture.WaitForBlockedWriteAsync();
        Assert.False(saving.IsCompleted);
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "after capture");
        Assert.False(saving.IsCompleted);
        await fixture.ReleaseWritesAsync();
        await saving.WaitAsync(Timeout);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_ExternalLoopStop_RejectsInsteadOfHanging()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.Loop.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync().WaitAsync(Timeout));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task SaveAsync_ImmediatelyFollowedByStop_DrainsAdmittedSave()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await using var fixture = await WorldSaveFixture.CreateAsync();
            await fixture.StartAsync();
            var saving = fixture.Saves.SaveAsync();
            var stopping = fixture.Saves.StopAsync(true);
            await Task.WhenAll(saving, stopping).WaitAsync(Timeout);
            Assert.Equal(2, fixture.Captures);
        }
    }

    [Fact]
    public async Task SaveAsync_OwnerCaptureReentry_RejectsBeforeJoiningItself()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        fixture.OnCapture = () =>
                            {
                                Action[] operations =
                                [
                                    () => { _ = fixture.Operations.ExecuteAsync(_ => Task.CompletedTask); },
                                    () => { _ = fixture.Saves.SaveAsync(); },
                                    () => { _ = fixture.Saves.StopAsync(true); }
                                ];

                                foreach (var operation in operations)
                                {
                                    Exception? error = null;

                                    try
                                    {
                                        operation();
                                    }
                                    catch (Exception exception)
                                    {
                                        error = exception;
                                    }

                                    Assert.IsType<InvalidOperationException>(error);
                                }
                            };
        await fixture.Saves.SaveAsync().WaitAsync(Timeout);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_PostCommitApplicationPending_WaitsBeforeCapture()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var apply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = fixture.Operations.ExecuteAsync(
            async token =>
            {
                await fixture.Items.UpsertAsync(new() { Id = fixture.Entities[0].Id, Name = "committed" }, token);
                committed.SetResult();
                await apply.Task;
                await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "committed");
            }
        );
        await committed.Task;
        var saving = fixture.Saves.SaveAsync();
        await fixture.OnLoopAsync(() => { });
        Assert.Equal("committed", await fixture.ReadSavedNameAsync());
        Assert.Equal(0, fixture.Captures);
        apply.SetResult();
        await Task.WhenAll(operation, saving).WaitAsync(Timeout);
        Assert.Equal("committed", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_PreCanceled_DoesNotInitiateCapture()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Saves.SaveAsync(cancellation.Token));
        await fixture.OnLoopAsync(() => { });
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StopAsync_CleanupOnly_DoesNotSaveAndRejectsLaterFinalUpgrade()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.Saves.StopAsync().WaitAsync(Timeout);
        await fixture.Saves.StopAsync().WaitAsync(Timeout);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.StopAsync(true));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StopAsync_DelayedPostCommitApplication_DrainsBeforeTerminalCaptureAndRejectsLateOperation()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var apply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = fixture.Operations.ExecuteAsync(
            async token =>
            {
                await fixture.Items.UpsertAsync(new() { Id = fixture.Entities[0].Id, Name = "final committed" }, token);
                committed.SetResult();
                await apply.Task;
                await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "final committed");
            }
        );
        await committed.Task;
        var stopping = fixture.Saves.StopAsync(true);

        try
        {
            Assert.False(stopping.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    fixture.Operations.ExecuteAsync(_ => throw new IOException("must not execute"))
            );
            await fixture.OnLoopAsync(() => { });
            Assert.Equal(0, fixture.Captures);
        }
        finally
        {
            apply.SetResult();
        }

        await Task.WhenAll(operation, stopping).WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
        Assert.Equal("final committed", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StopAsync_FinalSave_DrainsActiveSaveThenCapturesLatestQueuedMutationOnce()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(true);
        await fixture.StartAsync();
        await fixture.BlockWritesAsync();
        var active = fixture.Saves.SaveAsync();
        await fixture.WaitForBlockedWriteAsync();
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "latest");
        var stopping = fixture.Saves.StopAsync(true);
        Assert.False(stopping.IsCompleted);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
        await fixture.ReleaseWritesAsync();
        await Task.WhenAll(stopping, active).WaitAsync(Timeout);
        await fixture.Saves.StopAsync(true).WaitAsync(Timeout);
        Assert.Equal(2, fixture.Captures);
        Assert.False(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        Assert.Equal("latest", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StopAsync_LoopFault_ReportsOriginalFailureWithoutCapturing()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        var failure = new ApplicationException("fatal loop command");
        await fixture.Loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Loop.Completion.WaitAsync(Timeout)));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.StopAsync(true).WaitAsync(Timeout)));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StopAsync_PostCommitApplicationFails_SkipsUnsafeFinalCaptureAndStillStopsLoop()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync();
        await fixture.StartAsync();
        var failure = new IOException("owner application failed after commit");
        Assert.Same(
            failure,
            await Record.ExceptionAsync(
                () => fixture.Operations.ExecuteAsync(
                    async token =>
                    {
                        await fixture.Items.UpsertAsync(new() { Id = fixture.Entities[0].Id, Name = "committed" }, token);

                        throw failure;
                    }
                )
            )
        );
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.SaveAsync().WaitAsync(Timeout)));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.StopAsync(true).WaitAsync(Timeout)));
        Assert.Equal(0, fixture.Captures);
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
        Assert.Equal("committed", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StopAsync_TwoTargets_RejectsOrdinaryWorkBetweenTerminalCaptures()
    {
        await using var fixture = await WorldSaveFixture.CreateAsync(twoTargets: true);
        await fixture.StartAsync();
        await fixture.BlockWritesAsync(true);
        var stopping = fixture.Saves.StopAsync(true);

        try
        {
            await fixture.WaitForBlockedWriteAsync();
            Assert.Equal(1, fixture.Captures);
            Assert.False(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => fixture.Entities[0].Name = "lost mutation")));
        }
        finally
        {
            await fixture.ReleaseWritesAsync();
        }

        await stopping.WaitAsync(Timeout);
        Assert.Equal(2, fixture.Captures);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
        Assert.Equal("account", await fixture.AccountsDatabase!.ScalarAsync<string>("SELECT name FROM host_accounts.items"));
    }
}
