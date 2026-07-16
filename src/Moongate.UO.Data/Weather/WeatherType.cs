namespace Moongate.UO.Data.Weather;

/// <summary>
/// A named weather profile: per-effect chances and intensities (rain, snow, storm), temperature
/// bounds, and cold/heat chances.
/// </summary>
public sealed class WeatherType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RainChance { get; set; }
    public WeatherIntensity RainIntensity { get; set; } = new();
    public int RainTempDrop { get; set; }
    public int SnowChance { get; set; }
    public WeatherIntensity SnowIntensity { get; set; } = new();
    public int SnowThreshold { get; set; }
    public int StormChance { get; set; }
    public WeatherIntensity StormIntensity { get; set; } = new();
    public int StormTempDrop { get; set; }
    public int MaxTemp { get; set; }
    public int MinTemp { get; set; }
    public int ColdChance { get; set; }
    public int ColdIntensity { get; set; }
    public int HeatChance { get; set; }
    public int HeatIntensity { get; set; }
}
