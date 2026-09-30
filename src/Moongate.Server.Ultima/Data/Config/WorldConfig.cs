namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for the live world.
/// </summary>
public sealed class WorldConfig
{
    /// <summary>
    ///     Gets or sets how far players see mobiles and items, in cells along X or Y; the client is told with 0xC8.
    /// </summary>
    public int ViewRange { get; set; } = 18;

    /// <summary>
    ///     Gets or sets how many real seconds a game minute lasts; 5, as ModernUO, makes a game day last 2 real hours.
    /// </summary>
    public int SecondsPerUoMinute { get; set; } = 5;

    /// <summary>
    ///     Gets or sets the light level of the day, from 0 (brightest) to 31.
    /// </summary>
    public int DayLight { get; set; }

    /// <summary>
    ///     Gets or sets the light level of the night, from 0 (brightest) to 31.
    /// </summary>
    public int NightLight { get; set; } = 12;

    /// <summary>
    ///     Gets or sets the light level inside a dungeon region, from 0 (brightest) to 31; 26, as ModernUO.
    /// </summary>
    public int DungeonLight { get; set; } = 26;

    /// <summary>
    ///     Gets or sets the light level inside a jail region, from 0 (brightest) to 31; 9, as ModernUO.
    /// </summary>
    public int JailLight { get; set; } = 9;

    /// <summary>
    ///     Validates the section before server services begin startup: the range must be one the client supports, from
    ///     5 to 24, as ModernUO and POL allow; a game minute from 1 to 3600 seconds; the light levels from 0 to 31.
    /// </summary>
    public void Validate()
    {
        if (ViewRange is < 5 or > 24)
        {
            throw new InvalidOperationException($"ultima.world.view_range must be from 5 to 24, found {ViewRange}.");
        }

        if (SecondsPerUoMinute is < 1 or > 3600)
        {
            throw new InvalidOperationException(
                $"ultima.world.seconds_per_uo_minute must be from 1 to 3600, found {SecondsPerUoMinute}."
            );
        }

        if (DayLight is < 0 or > 31)
        {
            throw new InvalidOperationException($"ultima.world.day_light must be from 0 to 31, found {DayLight}.");
        }

        if (NightLight is < 0 or > 31)
        {
            throw new InvalidOperationException($"ultima.world.night_light must be from 0 to 31, found {NightLight}.");
        }

        if (DungeonLight is < 0 or > 31)
        {
            throw new InvalidOperationException($"ultima.world.dungeon_light must be from 0 to 31, found {DungeonLight}.");
        }

        if (JailLight is < 0 or > 31)
        {
            throw new InvalidOperationException($"ultima.world.jail_light must be from 0 to 31, found {JailLight}.");
        }
    }
}
