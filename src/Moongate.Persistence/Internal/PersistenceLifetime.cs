namespace Moongate.Persistence.Internal;

internal sealed class PersistenceLifetime
{
    private readonly Lock _sync = new();
    private int _active;
    private bool _closing;
    private TaskCompletionSource? _drained;
    private Task? _close;

    public Task CloseAsync(Func<Task> close)
    {
        lock (_sync)
        {
            if (_close is not null)
            {
                return _close;
            }

            _closing = true;
            var drained = _active == 0
                              ? Task.CompletedTask
                              : (_drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            _close = CloseCoreAsync(drained, close);

            return _close;
        }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            _active++;
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _active--;

                if (_closing && _active == 0)
                {
                    _drained?.TrySetResult();
                }
            }
        }
    }

    private static async Task CloseCoreAsync(Task drained, Func<Task> close)
    {
        await Task.Yield();
        await drained.ConfigureAwait(false);
        await close().ConfigureAwait(false);
    }
}
