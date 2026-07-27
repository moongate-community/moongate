namespace Moongate.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider" /> frozen at a fixed instant, in UTC, so time-dependent output is
/// deterministic in tests. <see cref="TimeProvider.GetLocalNow" /> returns the same instant.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
        => _now;
}
