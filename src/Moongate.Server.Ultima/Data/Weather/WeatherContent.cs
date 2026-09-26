namespace Moongate.Server.Ultima.Data.Weather;

/// <summary>
///     One weather profile of <c>weather.toml</c>. Chances are percentages; the precipitation chances are per hour
///     and checked storm first, then snow, then rain.
/// </summary>
public class WeatherContent
{
    /// <summary>
    ///     The name a region uses to pick this profile.
    /// </summary>
    public string Name { get; set; }

    public int RainChance { get; set; }

    public int SnowChance { get; set; }

    public int StormChance { get; set; }

    /// <summary>
    ///     It does not snow at this temperature or above.
    /// </summary>
    public int SnowThreshold { get; set; }

    public int MinTemperature { get; set; }

    public int MaxTemperature { get; set; }

    /// <summary>
    ///     The chance of a cold day, whose temperature is between <see cref="MinTemperature" /> and
    ///     <see cref="ColdTemperature" />.
    /// </summary>
    public int ColdChance { get; set; }

    public int ColdTemperature { get; set; }

    /// <summary>
    ///     The chance of a hot day, whose temperature is between <see cref="MaxTemperature" /> and
    ///     <see cref="HeatTemperature" />.
    /// </summary>
    public int HeatChance { get; set; }

    public int HeatTemperature { get; set; }

    public int RainTemperatureDrop { get; set; }

    public int StormTemperatureDrop { get; set; }
}
