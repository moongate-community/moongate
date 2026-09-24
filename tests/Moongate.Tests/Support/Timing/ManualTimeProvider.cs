namespace Moongate.Tests.Support.Timing;

public sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _advanceLock = new();
    private long _timestamp;
    private long _fractionalTimestampNumerator;

    public override long TimestampFrequency { get; }

    public ManualTimeProvider(long timestampFrequency = TimeSpan.TicksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);
        TimestampFrequency = timestampFrequency;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        lock (_advanceLock)
        {
            var numerator = (Int128)elapsed.Ticks * TimestampFrequency + _fractionalTimestampNumerator;
            var delta = checked((long)(numerator / TimeSpan.TicksPerSecond));
            var timestamp = checked(_timestamp + delta);
            _fractionalTimestampNumerator = (long)(numerator % TimeSpan.TicksPerSecond);
            Interlocked.Exchange(ref _timestamp, timestamp);
        }
    }

    public override long GetTimestamp()
        => Interlocked.Read(ref _timestamp);
}
