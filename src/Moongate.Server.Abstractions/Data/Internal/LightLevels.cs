namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// The global light level through the day. Higher is darker. Ported from ModernUO's
/// <c>LightCycle.ComputeLevelFor</c>, which follows RunUO's curve rather than OSI's: OSI switches
/// sharply at 04:00 and midnight, this ramps across two hours at each end.
/// </summary>
public static class LightLevels
{
    /// <summary>Full daylight.</summary>
    public const int Day = 0;

    /// <summary>Full night.</summary>
    public const int Night = 12;

    /// <summary>Inside a dungeon, whatever the hour.</summary>
    public const int Dungeon = 26;

    /// <summary>Inside a jail, whatever the hour.</summary>
    public const int Jail = 9;

    /// <summary>
    /// The level at a local time of day:
    /// <code>
    /// 00:00 - 03:59  night
    /// 04:00 - 05:59  ramping to day over 120 minutes
    /// 06:00 - 21:59  day
    /// 22:00 - 23:59  ramping to night over 120 minutes
    /// </code>
    /// </summary>
    public static int ForTime(int hours, int minutes)
        => hours switch
        {
            < 4  => Night,
            < 6  => Night + ((hours - 4) * 60 + minutes) * (Day - Night) / 120,
            < 22 => Day,
            < 24 => Day + ((hours - 22) * 60 + minutes) * (Night - Day) / 120,
            _    => Night
        };
}
