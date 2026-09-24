namespace Moongate.Persistence.Internal;

internal sealed class PersistenceCaptureState
{
    private const int AwaitingCapture = 0;
    private const int Capturing = 1;
    private const int Captured = 2;
    private const int Invalid = 3;
    private const int Closed = 4;

    private readonly Lock _sync = new();
    private int _state = AwaitingCapture;
    private TaskCompletionSource? _captureExited;

    public void BeginCapture()
    {
        lock (_sync)
        {
            if (_state == Closed)
            {
                throw new InvalidOperationException("The persistence capture action cannot run after its callback returns.");
            }

            if (_state != AwaitingCapture)
            {
                _state = Invalid;

                throw new InvalidOperationException("The persistence capture action must be invoked exactly once.");
            }

            _state = Capturing;
            _captureExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public async Task<bool> CloseAsync()
    {
        Task exited;
        bool valid;

        lock (_sync)
        {
            // Freeze the result at dispatcher exit: late success cannot repair an early return.
            valid = _state == Captured && _captureExited?.Task.IsCompleted == true;
            _state = Closed;
            exited = _captureExited?.Task ?? Task.CompletedTask;
        }

        // Cancellation must not release coordination while admitted source/snapshot code is running.
        await exited.ConfigureAwait(false);

        return valid;
    }

    public void CompleteCapture()
    {
        lock (_sync)
        {
            if (_state != Capturing)
            {
                if (_state != Closed)
                {
                    _state = Invalid;
                }

                throw new InvalidOperationException("The persistence capture action completed after its callback returned.");
            }

            _state = Captured;
        }
    }

    public void ExitCapture()
    {
        lock (_sync)
        {
            _captureExited?.TrySetResult();
        }
    }

    public void FailCapture()
    {
        lock (_sync)
        {
            if (_state != Closed)
            {
                _state = Invalid;
            }
        }
    }
}
