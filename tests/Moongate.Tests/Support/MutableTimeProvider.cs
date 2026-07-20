namespace Moongate.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider" /> whose current instant can be set or advanced, for tests that need to
/// move time forward deterministically. <see cref="TimeProvider.GetLocalNow" /> returns the same instant.
/// </summary>
public sealed class MutableTimeProvider : TimeProvider
{
    public MutableTimeProvider(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
        => Now;

    public void Advance(TimeSpan delta)
        => Now += delta;
}
