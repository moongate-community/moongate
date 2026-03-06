using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Spatial;

public sealed class LightService : ILightService
{
    private const int DayLevel = 0;
    private const int NightLevel = 12;
    private const int DungeonLevel = 26;
    private const int JailLevel = 9;
    private const int DefaultPersonalLightLevel = 0;
    private const double DefaultSecondsPerUoMinute = 5.0;
    private static readonly DateTime DefaultWorldStartUtc = new(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ITimerService _timerService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly Dictionary<long, int> _lastGlobalLightBySessionId = [];
    private readonly DateTime _worldStartUtc;
    private readonly double _secondsPerUoMinute;
    private int? _forcedGlobalLightLevel;

    public LightService(
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

    public int ComputeGlobalLightLevel(DateTime? utcNow = null)
    {
        if (_forcedGlobalLightLevel.HasValue)
        {
            return _forcedGlobalLightLevel.Value;
        }

        var now = utcNow?.ToUniversalTime() ?? DateTime.UtcNow;

        return ComputeLightLevelFromHourMinute(now.Hour, now.Minute);
    }

    public int ComputeGlobalLightLevel(int mapId, Point3D location, DateTime? utcNow = null)
    {
        if (_forcedGlobalLightLevel.HasValue)
        {
            return _forcedGlobalLightLevel.Value;
        }

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
        var normalizedMinutes = (totalMinutes % (24 * 60) + 24 * 60) % (24 * 60);
        var hour = normalizedMinutes / 60;
        var minute = normalizedMinutes % 60;

        return ComputeLightLevelFromHourMinute(hour, minute);
    }

    public void SetGlobalLightOverride(int? lightLevel, bool applyImmediately = true)
    {
        _forcedGlobalLightLevel = lightLevel.HasValue
                                      ? Math.Clamp(lightLevel.Value, 0, byte.MaxValue)
                                      : null;

        if (applyImmediately)
        {
            ProcessLight();
        }
    }

    public Task StartAsync()
    {
        _timerService.RegisterTimer("light_update", TimeSpan.FromSeconds(10), ProcessLight, TimeSpan.FromSeconds(10), true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
        => Task.CompletedTask;

    private static int ComputeLightLevelFromHourMinute(int hour, int minute)
        => hour switch
        {
            < 4  => NightLevel,
            < 6  => NightLevel + ((hour - 4) * 60 + minute) * (DayLevel - NightLevel) / 120,
            < 22 => DayLevel,
            < 24 => DayLevel + ((hour - 22) * 60 + minute) * (NightLevel - DayLevel) / 120,
            _    => NightLevel
        };

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
            var globalLightLevel = (LightLevelType)(byte)Math.Clamp(globalLight, 0, byte.MaxValue);
            var personalLightLevel = (LightLevelType)DefaultPersonalLightLevel;
            _outgoingPacketQueue.Enqueue(session.SessionId, new OverallLightLevelPacket(globalLightLevel));
            _outgoingPacketQueue.Enqueue(
                session.SessionId,
                new PersonalLightLevelPacket(personalLightLevel, session.Character)
            );
        }

        if (_lastGlobalLightBySessionId.Count == 0)
        {
            return;
        }

        var staleSessionIds = _lastGlobalLightBySessionId.Keys
                                                         .Where(sessionId => !activeSessionIds.Contains(sessionId))
                                                         .ToList();

        foreach (var staleSessionId in staleSessionIds)
        {
            _lastGlobalLightBySessionId.Remove(staleSessionId);
        }
    }
}
