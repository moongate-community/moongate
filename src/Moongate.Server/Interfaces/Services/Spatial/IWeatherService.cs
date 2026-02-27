using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Weather;

namespace Moongate.Server.Interfaces.Services.Spatial;

public interface IWeatherService : IMoongateService
{
    void SetWeatherTypes(List<JsonWeather> weatherTypes);
    WeatherSnapshot GenerateSnapshot(JsonWeather weather, Random? random = null);
}
