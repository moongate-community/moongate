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

    public void BeginCapture()
    {
        lock (_sync)
        {
            if (_state == Closed)
            {
                throw new InvalidOperationException(
                    "The persistence capture action cannot run after its callback returns."
                );
            }

            if (_state != AwaitingCapture)
            {
                _state = Invalid;
                throw new InvalidOperationException(
                    "The persistence capture action must be invoked exactly once."
                );
            }

            _state = Capturing;
        }
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

                throw new InvalidOperationException(
                    "The persistence capture action completed after its callback returned."
                );
            }

            _state = Captured;
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

    public bool Close()
    {
        lock (_sync)
        {
            var valid = _state == Captured;
            _state = Closed;

            return valid;
        }
    }
}
