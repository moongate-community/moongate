using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Weather;
using Serilog;

namespace Moongate.Server.Services.Spatial;

public class WeatherService : IWeatherService
{
    private readonly ITimerService _timerService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ILogger _logger = Log.ForContext<WeatherService>();
    private readonly List<JsonWeather> _weatherTypes = [];

    public WeatherService(
        ITimerService timerService,
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task StartAsync()
    {
        _timerService.RegisterTimer(
            "weather_update",
            TimeSpan.FromMinutes(5),
            OnWeatherCallBack,
            TimeSpan.FromMinutes(5),
            true
        );

        return Task.CompletedTask;
    }

    public Task StopAsync()
        => Task.CompletedTask;

    public void SetWeatherTypes(List<JsonWeather> weatherTypes)
    {
        _weatherTypes.Clear();
        _weatherTypes.AddRange(weatherTypes);
        _logger.Information("Weather types set: {Count} types loaded.", _weatherTypes.Count);
    }

    public WeatherSnapshot GenerateSnapshot(JsonWeather weather, Random? random = null)
    {
        random ??= Random.Shared;

        var baseTemperature = random.Next(weather.MinTemp, weather.MaxTemp + 1);
        var adjustedTemperature = baseTemperature;

        if (weather.ColdChance > 0 && random.Next(100) < weather.ColdChance)
        {
            adjustedTemperature -= weather.ColdIntensity;
        }
        else if (weather.HeatChance > 0 && random.Next(100) < weather.HeatChance)
        {
            adjustedTemperature += weather.HeatIntensity;
        }

        var condition = weather.DetermineJsonWeatherCondition(adjustedTemperature, random);
        var effectIntensity = weather.GetDamageIntensity(condition, random);
        var temperatureDrop = weather.GetTemperatureDrop(condition);
        var effectiveTemperature = adjustedTemperature - temperatureDrop;
        var weatherType = condition switch
        {
            JsonWeatherCondition.Rain  => WeatherType.Rain,
            JsonWeatherCondition.Snow  => WeatherType.Snow,
            JsonWeatherCondition.Storm => WeatherType.Storm,
            _                          => WeatherType.None
        };
        var effectCount = (byte)Math.Clamp(effectIntensity, 0, 70);

        return new(
            weather.Id,
            weather.Name,
            baseTemperature,
            adjustedTemperature,
            condition,
            effectIntensity,
            temperatureDrop,
            effectiveTemperature,
            weatherType,
            effectCount
        );
    }

    private void OnWeatherCallBack()
    {
        var activeSectors = _spatialWorldService.GetActiveSectors();

        foreach (var sector in activeSectors)
        {
            if (_weatherTypes.Count == 0)
            {
                _logger.Warning("No weather types available to generate weather snapshot.");

                continue;
            }

            var randomWeather = _weatherTypes[Random.Shared.Next(_weatherTypes.Count)];
            var snapshot = GenerateSnapshot(randomWeather);
            var weatherPacket = WeatherFactory.CreateFromSnapshot(snapshot);

            foreach (var mobile in sector.GetPlayers())
            {
                if (_gameNetworkSessionService.TryGetByCharacterId(mobile.Id, out var session))
                {
                    _outgoingPacketQueue.Enqueue(session.SessionId, weatherPacket);
                }
            }
        }
    }
}
