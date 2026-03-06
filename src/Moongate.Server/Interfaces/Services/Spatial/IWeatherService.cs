using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Weather;

namespace Moongate.Server.Interfaces.Services.Spatial;

/// <summary>
/// Provides weather configuration and weather snapshot generation services.
/// </summary>
public interface IWeatherService : IMoongateService
{
    /// <summary>
    /// Generates a concrete weather snapshot from a weather definition.
    /// </summary>
    /// <param name="weather">Source weather definition.</param>
    /// <param name="random">Optional random source for deterministic tests.</param>
    /// <returns>A concrete weather snapshot with resolved values.</returns>
    WeatherSnapshot GenerateSnapshot(JsonWeather weather, Random? random = null);

    /// <summary>
    /// Replaces the available weather type definitions used by the service.
    /// </summary>
    /// <param name="weatherTypes">Weather definitions loaded from JSON data files.</param>
    void SetWeatherTypes(List<JsonWeather> weatherTypes);
}
