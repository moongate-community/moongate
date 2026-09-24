namespace Moongate.Tests.TestSupport.Persistence;

internal sealed class WorldSaveTimeProvider : TimeProvider
{
    private long _timestamp;

    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan elapsed)
    {
        Interlocked.Add(ref _timestamp, elapsed.Ticks);
        UtcNow += elapsed;
    }

    public override long GetTimestamp()
        => Interlocked.Read(ref _timestamp);

    public override DateTimeOffset GetUtcNow()
        => UtcNow;
}
