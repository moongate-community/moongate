namespace Moongate.Server.Ultima.Data.Weather;

/// <summary>
///     The root of <c>weather.toml</c>: an array of tables under <c>weather</c>. The property name must match the table
///     name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class WeatherContentFile
{
    public List<WeatherContent> Weather { get; set; } = [];
}
