namespace Moongate.Tests.Support.Timing;

/// <summary>A monotonic clock advanced directly in provider units for sub-TimeSpan-tick boundary tests.</summary>
public sealed class RawTimestampTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency { get; }

    public RawTimestampTimeProvider(long timestampFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestampFrequency);
        TimestampFrequency = timestampFrequency;
    }

    public void AdvanceTimestamp(long elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed);
        long before;
        long after;

        do
        {
            before = Interlocked.Read(ref _timestamp);
            after = checked(before + elapsed);
        } while (Interlocked.CompareExchange(ref _timestamp, after, before) != before);
    }

    public override long GetTimestamp()
        => Interlocked.Read(ref _timestamp);
}
