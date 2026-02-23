namespace Moongate.UO.Data.Json.Weather;

/// <summary>
/// Represents JsonWeatherWrap.
/// </summary>
public class JsonWeatherWrap
{
    public JsonDfnHeader Header { get; set; }
    public List<JsonWeather> WeatherTypes { get; set; } = new();
}
