using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     UOX3's weather rolls for a profile of <c>weather.toml</c>: the temperature of each day and what falls each hour.
/// </summary>
internal static class WeatherRolls
{
    // ModernUO's densest weather.
    public const int MaxDensity = 70;
    private const int MinDensity = 10;

    /// <summary>
    ///     A hot day (between the maximum and the heat temperature), a cold day (between the cold temperature and the
    ///     minimum) or a normal one (between the minimum and the maximum).
    /// </summary>
    public static int DayTemperature(WeatherContent profile, Random random)
    {
        if (Hits(profile.HeatChance, random))
        {
            return random.Next(profile.MaxTemperature, Math.Max(profile.HeatTemperature, profile.MaxTemperature) + 1);
        }

        if (Hits(profile.ColdChance, random))
        {
            return random.Next(Math.Min(profile.ColdTemperature, profile.MinTemperature), profile.MinTemperature + 1);
        }

        return random.Next(profile.MinTemperature, Math.Max(profile.MinTemperature, profile.MaxTemperature) + 1);
    }

    /// <summary>
    ///     A storm first, then snow when the day is colder than the snow threshold, then rain, else dry; rain and storm
    ///     lower the day's temperature by their drop.
    /// </summary>
    public static WeatherState Hour(WeatherContent profile, int dayTemperature, Random random)
    {
        if (Hits(profile.StormChance, random))
        {
            return Falling(WeatherKindType.Storm, dayTemperature - profile.StormTemperatureDrop, random);
        }

        if (Hits(profile.SnowChance, random) && dayTemperature < profile.SnowThreshold)
        {
            return Falling(WeatherKindType.Snow, dayTemperature, random);
        }

        if (Hits(profile.RainChance, random))
        {
            return Falling(WeatherKindType.Rain, dayTemperature - profile.RainTemperatureDrop, random);
        }

        return new(WeatherKindType.None, 0, Clamp(dayTemperature));
    }

    private static WeatherState Falling(WeatherKindType kind, int temperature, Random random)
    {
        return new(kind, random.Next(MinDensity, MaxDensity + 1), Clamp(temperature));
    }

    private static bool Hits(int chance, Random random)
    {
        return chance > 0 && random.Next(1, 101) <= chance;
    }

    private static int Clamp(int temperature)
    {
        return Math.Clamp(temperature, sbyte.MinValue, sbyte.MaxValue);
    }
}
