using Moongate.Server.Ultima.Types.Weather;

namespace Moongate.Server.Ultima.Data.Weather;

/// <summary>
///     The weather of a profile for the current game hour: what falls, how many particles, the temperature.
/// </summary>
public sealed record WeatherState(WeatherKindType Kind, int Density, int Temperature);
