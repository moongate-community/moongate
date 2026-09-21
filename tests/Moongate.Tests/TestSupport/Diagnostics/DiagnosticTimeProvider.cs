namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class DiagnosticTimeProvider : TimeProvider, IDisposable
{
    private readonly Lock _gate = new();
    private DateTimeOffset _utcNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private long _timestamp;
    private ManualDiagnosticTimer? _timer;

    public Exception? TimestampFailure { get; set; }
    public int TimerCount { get; private set; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _utcNow;
    }

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            if (TimestampFailure is not null) throw TimestampFailure;
            return _timestamp;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            TimerCount++;
            return _timer = new ManualDiagnosticTimer(callback, state, dueTime, period);
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        ManualDiagnosticTimer? timer;
        lock (_gate)
        {
            _utcNow += elapsed;
            _timestamp += elapsed.Ticks;
            timer = _timer;
        }

        timer?.Tick();
    }

    public void Dispose()
    {
        ManualDiagnosticTimer? timer;
        lock (_gate) timer = _timer;
        timer?.Dispose();
    }
}
