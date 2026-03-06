using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Weather;
using Serilog;

namespace Moongate.Server.Services.Spatial;

public class WeatherService : IWeatherService
{
    private const int DayLevel = 0;
    private const int NightLevel = 12;
    private const int DungeonLevel = 26;
    private const int JailLevel = 9;
    private const double DefaultSecondsPerUoMinute = 5.0;
    private static readonly DateTime DefaultWorldStartUtc = new(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ITimerService _timerService;

    private readonly ILogger _logger = Log.ForContext<WeatherService>();

    private readonly List<JsonWeather> _weatherTypes = new();

    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly Dictionary<long, int> _lastGlobalLightBySessionId = [];
    private readonly DateTime _worldStartUtc;
    private readonly double _secondsPerUoMinute;

    public WeatherService(
        ITimerService timerService,
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService,
        MoongateSpatialConfig? spatialConfig = null
    )
    {
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;

        _secondsPerUoMinute = spatialConfig is { LightSecondsPerUoMinute: > 0 }
            ? spatialConfig.LightSecondsPerUoMinute
            : DefaultSecondsPerUoMinute;

        if (!DateTime.TryParse(spatialConfig?.LightWorldStartUtc, out var parsedWorldStart))
        {
            _worldStartUtc = DefaultWorldStartUtc;
            return;
        }

        _worldStartUtc = parsedWorldStart.ToUniversalTime();
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

    public Task StartAsync()
    {
        _timerService.RegisterTimer(
            "weather_update",
            TimeSpan.FromMinutes(5),
            OnWeatherCallBack,
            TimeSpan.FromMinutes(5),
            true
        );


        _timerService.RegisterTimer("light_update", TimeSpan.FromSeconds(10), ProcessLight, TimeSpan.FromSeconds(10), true);

        return Task.CompletedTask;
    }

    private void ProcessLight()
    {
        var activeSessionIds = new HashSet<long>();
        foreach (var session in _gameNetworkSessionService.GetAll())
        {
            if (session.Character is null)
            {
                continue;
            }

            activeSessionIds.Add(session.SessionId);

            var globalLight = ComputeGlobalLightLevel(session.Character.MapId, session.Character.Location);
            if (_lastGlobalLightBySessionId.TryGetValue(session.SessionId, out var lastGlobalLight) &&
                lastGlobalLight == globalLight)
            {
                continue;
            }

            _lastGlobalLightBySessionId[session.SessionId] = globalLight;
            var lightLevel = (LightLevelType)(byte)Math.Clamp(globalLight, 0, byte.MaxValue);
            _outgoingPacketQueue.Enqueue(session.SessionId, new OverallLightLevelPacket(lightLevel));
            _outgoingPacketQueue.Enqueue(session.SessionId, new PersonalLightLevelPacket(lightLevel, session.Character));
        }

        if (_lastGlobalLightBySessionId.Count == 0)
        {
            return;
        }

        var staleSessionIds = _lastGlobalLightBySessionId.Keys.Where(sessionId => !activeSessionIds.Contains(sessionId)).ToList();
        foreach (var staleSessionId in staleSessionIds)
        {
            _lastGlobalLightBySessionId.Remove(staleSessionId);
        }
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

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

    public int ComputeGlobalLightLevel(DateTime? utcNow = null)
    {
        var now = utcNow?.ToUniversalTime() ?? DateTime.UtcNow;
        return ComputeLightLevelFromHourMinute(now.Hour, now.Minute);
    }

    public int ComputeGlobalLightLevel(int mapId, Point3D location, DateTime? utcNow = null)
    {
        var region = _spatialWorldService.ResolveRegion(mapId, location);
        if (region is JsonDungeonRegion)
        {
            return DungeonLevel;
        }

        if (region is JsonJailRegion)
        {
            return JailLevel;
        }

        var now = utcNow?.ToUniversalTime() ?? DateTime.UtcNow;
        var totalMinutes = (int)((now - _worldStartUtc).TotalSeconds / _secondsPerUoMinute);
        totalMinutes += mapId * 320;
        totalMinutes += location.X / 16;
        var normalizedMinutes = ((totalMinutes % (24 * 60)) + (24 * 60)) % (24 * 60);
        var hour = normalizedMinutes / 60;
        var minute = normalizedMinutes % 60;

        return ComputeLightLevelFromHourMinute(hour, minute);
    }

    private static int ComputeLightLevelFromHourMinute(int hour, int minute)
    {
        return hour switch
        {
            < 4  => NightLevel,
            < 6  => NightLevel + ((hour - 4) * 60 + minute) * (DayLevel - NightLevel) / 120,
            < 22 => DayLevel,
            < 24 => DayLevel + ((hour - 22) * 60 + minute) * (NightLevel - DayLevel) / 120,
            _    => NightLevel
        };
    }
}
