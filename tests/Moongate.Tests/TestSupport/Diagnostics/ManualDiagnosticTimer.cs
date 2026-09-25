namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class ManualDiagnosticTimer : ITimer
{
    private readonly TimerCallback _callback;
    private readonly object? _state;
    private readonly Lock _gate = new();
    private bool _disposed;

    public TimeSpan DueTime { get; private set; }
    public TimeSpan Period { get; private set; }

    public ManualDiagnosticTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _callback = callback;
        _state = state;
        DueTime = dueTime;
        Period = period;
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            DueTime = dueTime;
            Period = period;

            return true;
        }
    }

    public void Tick()
    {
        lock (_gate)
        {
            if (!_disposed && DueTime != Timeout.InfiniteTimeSpan)
            {
                _callback(_state);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
