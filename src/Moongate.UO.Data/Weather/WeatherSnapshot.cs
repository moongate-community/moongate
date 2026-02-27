using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Weather;

/// <summary>
/// Materialized weather state resolved from a <see cref="JsonWeather" /> definition.
/// </summary>
public readonly record struct WeatherSnapshot(
    int WeatherId,
    string WeatherName,
    int BaseTemperature,
    int AdjustedTemperature,
    JsonWeatherCondition Condition,
    int EffectIntensity,
    int TemperatureDrop,
    int EffectiveTemperature,
    WeatherType Type,
    byte EffectCount
);
