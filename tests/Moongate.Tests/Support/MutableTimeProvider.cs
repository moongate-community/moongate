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

    /// <summary>
    /// A clock starting at the real instant, which is what any test involving a JWT needs.
    /// <para>
    /// The API issues tokens from the injected provider but validates their lifetime against the system
    /// clock, because <c>TokenValidationParameters</c> offers no way to supply one. In production both are
    /// the system clock and the two agree; in a test they only agree if this clock starts near the real
    /// instant. A hardcoded date makes the suite pass or fail depending on the hour it is run — which is
    /// exactly what it did before this existed.
    /// </para>
    /// </summary>
    public static MutableTimeProvider StartingNow()
        => new(DateTimeOffset.UtcNow);

    public DateTimeOffset Now { get; set; }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
        => Now;

    public void Advance(TimeSpan delta)
        => Now += delta;
}
