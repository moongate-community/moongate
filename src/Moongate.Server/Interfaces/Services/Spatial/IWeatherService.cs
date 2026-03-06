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
    /// Replaces the available weather type definitions used by the service.
    /// </summary>
    /// <param name="weatherTypes">Weather definitions loaded from JSON data files.</param>
    void SetWeatherTypes(List<JsonWeather> weatherTypes);

    /// <summary>
    /// Generates a concrete weather snapshot from a weather definition.
    /// </summary>
    /// <param name="weather">Source weather definition.</param>
    /// <param name="random">Optional random source for deterministic tests.</param>
    /// <returns>A concrete weather snapshot with resolved values.</returns>
    WeatherSnapshot GenerateSnapshot(JsonWeather weather, Random? random = null);

    /// <summary>
    /// Computes global light level using day/night cycle rules inspired by ModernUO LightCycle.
    /// </summary>
    /// <param name="utcNow">Optional UTC timestamp. Uses current UTC time when omitted.</param>
    /// <returns>Global light level byte (0 = day, higher = darker).</returns>
    int ComputeGlobalLightLevel(DateTime? utcNow = null);

    /// <summary>
    /// Computes global light level for the provided map/location using ModernUO Clock/GetTime style offsets.
    /// Dungeon and jail regions can override the global level.
    /// </summary>
    /// <param name="mapId">Map id.</param>
    /// <param name="location">World location.</param>
    /// <param name="utcNow">Optional UTC timestamp. Uses current UTC time when omitted.</param>
    /// <returns>Global light level byte (0 = day, higher = darker).</returns>
    int ComputeGlobalLightLevel(int mapId, Moongate.UO.Data.Geometry.Point3D location, DateTime? utcNow = null);
}
