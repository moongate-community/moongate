using Moongate.Server.Services.Persistence;

namespace Moongate.Tests.Server.Services.Persistence;

public sealed class PersistenceOperationBarrierTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task ExecuteAsync_AdmittedCallbackFails_PoisonsQueuedAndFutureWork(bool cancel)
    {
        var barrier = new PersistenceOperationBarrier();
        Exception failure = cancel ? new OperationCanceledException() : new IOException("post-commit apply failed");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = barrier.ExecuteAsync(async _ =>
            {
                await release.Task;

                throw failure;
            }
        );
        var ran = false;
        var queued = barrier.ExecuteAsync(_ =>
            {
                ran = true;

                return Task.CompletedTask;
            }
        );
        release.SetResult();
        Assert.Same(failure, await Record.ExceptionAsync(() => operation));
        Assert.Same(failure, await Record.ExceptionAsync(() => queued));
        Assert.False(ran);
        Assert.Same(failure, await Record.ExceptionAsync(() => barrier.RunSaveAsync(() => Task.CompletedTask)));
        Assert.Same(failure, await Record.ExceptionAsync(barrier.CloseAsync));
    }

    [Fact]
    public async Task ExecuteAsync_CanceledWhileQueued_DoesNotRunAndPoisonsCapture()
    {
        var barrier = new PersistenceOperationBarrier();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = barrier.ExecuteAsync(_ => release.Task);
        using var cancellation = new CancellationTokenSource();
        var ran = false;
        var queued = barrier.ExecuteAsync(
            _ =>
            {
                ran = true;

                return Task.CompletedTask;
            },
            cancellation.Token
        );
        cancellation.Cancel();
        release.SetResult();
        await first;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.False(ran);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => barrier.RunSaveAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task ExecuteAsync_OwnerApplicationPending_ExcludesSaveAndDrainsOnClose()
    {
        var barrier = new PersistenceOperationBarrier();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = barrier.ExecuteAsync(async _ =>
            {
                entered.SetResult();
                await release.Task;
            }
        );
        await entered.Task;
        var captured = false;
        var save = barrier.RunSaveAsync(() =>
            {
                captured = true;

                return Task.CompletedTask;
            }
        );
        var closing = barrier.CloseAsync();
        Assert.False(captured);
        Assert.False(closing.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => barrier.ExecuteAsync(_ => Task.CompletedTask));
        release.SetResult();
        await Task.WhenAll(operation, save, closing);
        Assert.True(captured);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledAndSaveFailure_DoNotPoison()
    {
        var barrier = new PersistenceOperationBarrier();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => barrier.ExecuteAsync(
                _ => Task.CompletedTask,
                new(true)
            )
        );
        await Assert.ThrowsAsync<IOException>(() => barrier.RunSaveAsync(() => throw new IOException()));
        await barrier.ExecuteAsync(_ => Task.CompletedTask);
        await barrier.CloseAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Reentry_RejectsWithoutDeadlock()
    {
        var barrier = new PersistenceOperationBarrier();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            barrier.ExecuteAsync(token => barrier.ExecuteAsync(_ => Task.CompletedTask, token))
        );
    }
}
