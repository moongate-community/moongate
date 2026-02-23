namespace Moongate.Server.Data.Events;

/// <summary>
/// Provides a shared clock utility for game event timestamps.
/// </summary>
public static class GameEventClock
{
    /// <summary>
    /// Gets current Unix timestamp in milliseconds.
    /// </summary>
    public static long UnixMillisecondsNow()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
