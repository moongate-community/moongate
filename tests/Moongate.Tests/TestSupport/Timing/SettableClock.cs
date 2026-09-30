namespace Moongate.Tests.TestSupport.Timing;

/// <summary>
///     A clock whose UTC time a test sets and advances.
/// </summary>
public sealed class SettableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }

    public void Advance(TimeSpan elapsed)
    {
        Now += elapsed;
    }
}
