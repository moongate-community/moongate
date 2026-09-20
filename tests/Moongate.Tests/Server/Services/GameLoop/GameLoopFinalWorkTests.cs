using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Interfaces.GameLoop;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;

namespace Moongate.Tests.Server.Services.GameLoop;

public sealed class GameLoopFinalWorkTests
{
    [Fact]
    public async Task StopWithFinalWorkAsync_TwoCaptures_DrainsOrdinaryWorkAndClosesAdmission()
    {
        using var loop = Create();
        await loop.StartAsync();
        var values = new List<int>();
        await loop.PostAsync(new ActionGameLoopWorkItem(() => values.Add(1)));
        Func<IGameLoopWorkItem, Task>? escaped = null;
        await loop.StopWithFinalWorkAsync(async (dispatch, _) =>
        {
            escaped = dispatch;
            Assert.False(loop.IsOnLoopThread);
            await dispatch(new ActionGameLoopWorkItem(() => { Assert.True(loop.IsOnLoopThread); values.Add(2); }));
            Assert.False(loop.TryPost(new ActionGameLoopWorkItem(() => values.Add(99))));
            await Task.Yield();
            await dispatch(new ActionGameLoopWorkItem(() => { Assert.True(loop.IsOnLoopThread); values.Add(3); }));
        }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([1, 2, 3], values);
        Assert.True(loop.Completion.IsCompletedSuccessfully);
        await Assert.ThrowsAsync<InvalidOperationException>(() => escaped!(new ActionGameLoopWorkItem(() => { })));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task StopWithFinalWorkAsync_CallbackFails_ClosesLoopAndRetainsFailure(bool cancel)
    {
        using var loop = Create();
        await loop.StartAsync();
        Exception failure = cancel ? new OperationCanceledException() : new IOException("database failure");
        Assert.Same(failure, await Record.ExceptionAsync(() => loop.StopWithFinalWorkAsync((_, _) => throw failure).WaitAsync(TimeSpan.FromSeconds(10))));
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopWithFinalWorkAsync_ConcurrentDispatcher_RejectsAndDrainsAdmittedCapture()
    {
        using var loop = Create();
        await loop.StartAsync();
        using var item = new BlockingGameLoopWorkItem();
        await loop.StopWithFinalWorkAsync(async (dispatch, _) =>
        {
            var first = dispatch(item);
            await item.Entered.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            try { await Assert.ThrowsAsync<InvalidOperationException>(() => dispatch(new ActionGameLoopWorkItem(() => { }))); }
            finally { item.Release(); }
            await first;
        }).WaitAsync(TimeSpan.FromSeconds(10));
    }


    [Theory, InlineData("fault"), InlineData("cancel")]
    public async Task StopWithFinalWorkAsync_CallbackExitsDuringCapture_DrainsBeforeJoiningAndPreservesCause(string exit)
    {
        using var loop = Create();
        await loop.StartAsync();
        using var blocker = new BlockingGameLoopWorkItem();
        var callbackExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? capture = null;
        Exception failure = exit == "cancel" ? new OperationCanceledException() : new IOException("terminal callback failed");
        var stopping = loop.StopWithFinalWorkAsync(async (dispatch, _) =>
        {
            capture = dispatch(blocker);
            await blocker.Entered.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            callbackExited.SetResult();
            throw failure;
        });
        await callbackExited.Task;
        Assert.False(stopping.IsCompleted);
        Assert.False(loop.Completion.IsCompleted);
        blocker.Release();
        var observed = await Record.ExceptionAsync(() => stopping.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Same(failure, observed);
        Assert.True(capture!.IsCompletedSuccessfully);
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopWithFinalWorkAsync_CancellationBetweenCaptures_DrainsAndClosesDispatcher()
    {
        using var loop = Create();
        await loop.StartAsync();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.StopWithFinalWorkAsync(async (dispatch, token) =>
        {
            await dispatch(new ActionGameLoopWorkItem(() => { }));
            cancellation.Cancel();
            await dispatch(new ActionGameLoopWorkItem(() => throw new IOException("must not execute")));
        }, cancellation.Token));
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task StopWithFinalWorkAsync_CaughtCaptureFailuresThenSuccess_RetainsEveryFailure()
    {
        using var loop = Create();
        await loop.StartAsync();
        var first = new IOException("first capture failed");
        var second = new InvalidOperationException("second capture failed");
        var succeeded = false;
        var observed = await Record.ExceptionAsync(() => loop.StopWithFinalWorkAsync(async (dispatch, _) =>
        {
            foreach (var failure in new Exception[] { first, second, first })
            {
                Assert.Same(failure, await Record.ExceptionAsync(() => dispatch(new ActionGameLoopWorkItem(() => throw failure))));
            }
            await dispatch(new ActionGameLoopWorkItem(() => succeeded = true));
        }).WaitAsync(TimeSpan.FromSeconds(10)));
        var aggregate = Assert.IsType<AggregateException>(observed);
        Assert.Collection(aggregate.InnerExceptions, failure => Assert.Same(first, failure), failure => Assert.Same(second, failure));
        Assert.True(succeeded);
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task StopWithFinalWorkAsync_CallbackAndCaptureFail_RetainsDistinctCausesAndJoins(bool sameFailure)
    {
        using var loop = Create();
        await loop.StartAsync();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackFailure = new IOException("callback failed");
        Exception captureFailure = sameFailure ? callbackFailure : new InvalidOperationException("capture failed");
        Task? capture = null;
        var stopping = loop.StopWithFinalWorkAsync(async (dispatch, _) =>
        {
            capture = dispatch(new ActionGameLoopWorkItem(() =>
            {
                entered.SetResult();
                release.Wait();
                Assert.True(loop.IsOnLoopThread);
                throw captureFailure;
            }));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
            callbackExited.SetResult();
            throw callbackFailure;
        });
        try
        {
            await callbackExited.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(stopping.IsCompleted);
            Assert.False(loop.Completion.IsCompleted);
        }
        finally { release.Set(); }
        var observed = await Record.ExceptionAsync(() => stopping.WaitAsync(TimeSpan.FromSeconds(10)));
        if (sameFailure)
        {
            Assert.Same(callbackFailure, observed);
        }
        else
        {
            var aggregate = Assert.IsType<AggregateException>(observed);
            Assert.Collection(aggregate.InnerExceptions, failure => Assert.Same(callbackFailure, failure), failure => Assert.Same(captureFailure, failure));
        }
        Assert.Same(captureFailure, await Record.ExceptionAsync(() => capture!));
        Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    private static GameLoopService Create()
    {
        return new GameLoopService(new GameLoopOptions(), new TimerWheelService(new TimerWheelOptions(), TimeProvider.System), TimeProvider.System);
    }
}
