namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// Turns real time into the world's local time. Ported from ModernUO's <c>Clock</c>: five real
/// seconds are one UO minute, so a UO day is two real hours, and the time is local — sixteen tiles
/// east is one UO minute later, which is why dawn sweeps across a map rather than arriving at once.
/// </summary>
public static class GameClock
{
    /// <summary>Real seconds per UO minute. ModernUO's <c>Clock.SecondsPerUOMinute</c>.</summary>
    public const double SecondsPerUoMinute = 5.0;

    /// <summary>Tiles east per UO minute of local time.</summary>
    public const int TilesPerUoMinute = 16;

    /// <summary>UO minutes each facet is offset from the last.</summary>
    public const int MinutesPerFacet = 320;

    /// <summary>
    /// Where the world's calendar starts. Fixed rather than taken from server start on purpose: the
    /// time of day is then absolute, and a restart does not jump it.
    /// </summary>
    public static readonly DateTimeOffset Epoch = new(1997, 9, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The local time at <paramref name="x" /> on <paramref name="mapId" />.</summary>
    public static (int Hours, int Minutes) LocalTime(DateTimeOffset now, int mapId, int x)
    {
        var totalMinutes = (int)((now - Epoch).TotalSeconds / SecondsPerUoMinute);

        totalMinutes += mapId * MinutesPerFacet;
        totalMinutes += x / TilesPerUoMinute;

        return (totalMinutes / 60 % 24, totalMinutes % 60);
    }
}
