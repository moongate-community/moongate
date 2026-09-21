namespace Moongate.Persistence.Internal;

internal sealed class PersistenceMutationGate : IDisposable
{
    private readonly Lock _lifecycleSync = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _acceptedOperations;
    private bool _closing;
    private Task? _closeTask;
    private TaskCompletionSource? _drained;

    public Task CloseAsync(Func<Task> close)
    {
        ArgumentNullException.ThrowIfNull(close);

        lock (_lifecycleSync)
        {
            if (_closeTask is not null)
            {
                return _closeTask;
            }

            _closing = true;
            var drained = _acceptedOperations == 0
                              ? Task.CompletedTask
                              : (_drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            _closeTask = CloseCoreAsync(drained, close);

            return _closeTask;
        }
    }

    public Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task wait;

        lock (_lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            _acceptedOperations++;
            wait = _semaphore.WaitAsync(cancellationToken);
        }

        return RunCoreAsync(wait, operation, cancellationToken);
    }

    public Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task wait;

        lock (_lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            _acceptedOperations++;
            wait = _semaphore.WaitAsync(cancellationToken);
        }

        return RunCoreAsync(wait, operation, cancellationToken);
    }

    private async Task CloseCoreAsync(Task drained, Func<Task> close)
    {
        await Task.Yield();
        await drained.ConfigureAwait(false);

        try
        {
            await close().ConfigureAwait(false);
        }
        finally
        {
            Dispose();
        }
    }

    private void CompleteOperation()
    {
        TaskCompletionSource? drained = null;

        lock (_lifecycleSync)
        {
            _acceptedOperations--;

            if (_closing && _acceptedOperations == 0)
            {
                drained = _drained;
            }
        }

        drained?.TrySetResult();
    }

    private async Task RunCoreAsync(
        Task wait,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();
        var acquired = false;

        try
        {
            await wait.ConfigureAwait(false);
            acquired = true;
            await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }

            CompleteOperation();
        }
    }

    private async Task<T> RunCoreAsync<T>(
        Task wait,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken
    )
    {
        await Task.Yield();
        var acquired = false;

        try
        {
            await wait.ConfigureAwait(false);
            acquired = true;

            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }

            CompleteOperation();
        }
    }

    public void Dispose()
        => _semaphore.Dispose();
}
