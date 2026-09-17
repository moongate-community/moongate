using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Services.Persistence;

public sealed class WorldSaveServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SaveAsync_BeforeActivation_RejectsAndDoesNotScheduleAutosave()
    {
        await using var fixture = new WorldSaveFixture(autosave: true);
        await fixture.StartAsync(activate: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        Assert.Equal(0, fixture.Captures);
        fixture.Saves.Activate();
        await fixture.Saves.SaveAsync().WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
    }

    [Fact]
    public async Task SaveAsync_DurableFlushBlocked_KeepsCapturedBytesAndAllowsLoopWork()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        fixture.FileSystem.BlockFlush = true;
        var saving = fixture.Saves.SaveAsync();
        await fixture.FileSystem.FlushEntered.Task.WaitAsync(Timeout);
        Assert.False(saving.IsCompleted);
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "after capture");
        Assert.False(saving.IsCompleted);
        fixture.FileSystem.Release();
        await saving.WaitAsync(Timeout);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWaitersJoinOneCapture_CallerCancellationOnlyCancelsWait()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        fixture.FileSystem.BlockFlush = true;
        using var cancellation = new CancellationTokenSource();
        var first = fixture.Saves.SaveAsync(cancellation.Token);
        await fixture.FileSystem.FlushEntered.Task.WaitAsync(Timeout);
        var second = fixture.Saves.SaveAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
        Assert.False(second.IsCompleted);
        fixture.FileSystem.Release();
        await second.WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task SaveAsync_PreCanceled_DoesNotInitiateCapture()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Saves.SaveAsync(cancellation.Token));
        await fixture.OnLoopAsync(() => { });
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task Autosave_DueCallback_StartsSupervisedWorkerAndLeavesLoopResponsive()
    {
        await using var fixture = new WorldSaveFixture(autosave: true);
        await fixture.StartAsync();
        fixture.FileSystem.BlockFlush = true;
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        await fixture.OnLoopAsync(() => { });
        await fixture.FileSystem.FlushEntered.Task.WaitAsync(Timeout);
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "still responsive");
        var joined = fixture.Saves.SaveAsync();
        fixture.FileSystem.Release();
        await joined.WaitAsync(Timeout);
        Assert.Equal(1, fixture.Captures);
        Assert.Equal("before", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StopAsync_FinalSave_DrainsActiveSaveThenCapturesLatestQueuedMutationOnce()
    {
        await using var fixture = new WorldSaveFixture(autosave: true);
        await fixture.StartAsync();
        fixture.FileSystem.BlockFlush = true;
        var active = fixture.Saves.SaveAsync();
        await fixture.FileSystem.FlushEntered.Task.WaitAsync(Timeout);
        await fixture.OnLoopAsync(() => fixture.Entities[0].Name = "latest");
        var stopping = fixture.Saves.StopAsync(saveFinal: true);
        Assert.False(stopping.IsCompleted);
        Assert.Equal(0, fixture.Timers.GetMetricsSnapshot().ActiveTimers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync());
        fixture.FileSystem.Release();
        await Task.WhenAll(stopping, active).WaitAsync(Timeout);
        await fixture.Saves.StopAsync(saveFinal: true).WaitAsync(Timeout);
        Assert.Equal(2, fixture.Captures);
        Assert.False(fixture.Loop.TryPost(new ActionGameLoopWorkItem(() => { })));
        Assert.Equal("latest", await fixture.ReadSavedNameAsync());
    }

    [Fact]
    public async Task StopAsync_CleanupOnly_DoesNotSaveAndRejectsLaterFinalUpgrade()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        await fixture.Saves.StopAsync().WaitAsync(Timeout);
        await fixture.Saves.StopAsync().WaitAsync(Timeout);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.StopAsync(saveFinal: true));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task StopAsync_LoopFault_ReportsOriginalFailureWithoutCapturing()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        var failure = new ApplicationException("fatal loop command");
        await fixture.Loop.PostAsync(new ActionGameLoopWorkItem(() => throw failure));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Loop.Completion.WaitAsync(Timeout)));
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.StopAsync(true).WaitAsync(Timeout)));
        Assert.Equal(0, fixture.Captures);
    }

    [Fact]
    public async Task SaveAsync_CaptureFailure_PropagatesWithoutKillingLoop_AndStopStillClosesLoop()
    {
        await using var fixture = new WorldSaveFixture();
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
    public async Task SaveAsync_ExternalLoopStop_RejectsInsteadOfHanging()
    {
        await using var fixture = new WorldSaveFixture();
        await fixture.StartAsync();
        await fixture.Loop.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Saves.SaveAsync().WaitAsync(Timeout));
        Assert.Equal(0, fixture.Captures);
    }
    [Fact]
    public async Task SaveAsync_AdmittedCaptureAbandonedByLoopFault_CompletesWithOriginalFailure()
    {
        await using var fixture = new WorldSaveFixture();
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
    public async Task Autosave_OverlappingTicks_ShareOneCaptureAndStopReportsDurableFailure()
    {
        await using var fixture = new WorldSaveFixture(autosave: true);
        await fixture.StartAsync();
        fixture.FileSystem.BlockFlush = true;
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.OnLoopAsync(() => { });
        Assert.Equal(0, fixture.Captures);
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.OnLoopAsync(() => { });
        await fixture.FileSystem.FlushEntered.Task.WaitAsync(Timeout);
        fixture.Clock.Advance(TimeSpan.FromSeconds(20));
        await fixture.OnLoopAsync(() => { });
        await fixture.OnLoopAsync(() => { });
        var joined = fixture.Saves.SaveAsync();
        var failure = new IOException("durable flush failed");
        fixture.FileSystem.FlushFailure = failure;
        fixture.FileSystem.Release();
        Assert.Same(failure, await Record.ExceptionAsync(() => joined.WaitAsync(Timeout)));
        Assert.Equal(1, fixture.Captures);
        Assert.Same(failure, await Record.ExceptionAsync(() => fixture.Saves.StopAsync().WaitAsync(Timeout)));
        Assert.True(fixture.Loop.Completion.IsCompletedSuccessfully);
    }

}
