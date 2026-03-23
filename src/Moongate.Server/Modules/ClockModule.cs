using System.Globalization;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Modules;

[ScriptModule("clock", "Provides ModernUO-style in-game clock descriptions for scripts.")]

/// <summary>
/// Exposes in-game clock helpers to Lua item scripts.
/// </summary>
public sealed class ClockModule
{
    private const double DefaultSecondsPerUoMinute = 5.0;
    private static readonly DateTime DefaultWorldStartUtc = new(1997, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService? _spatialWorldService;
    private readonly DateTime _worldStartUtc;
    private readonly double _secondsPerUoMinute;

    public ClockModule(
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService? spatialWorldService = null,
        MoongateSpatialConfig? spatialConfig = null
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _secondsPerUoMinute = spatialConfig is { LightSecondsPerUoMinute: > 0 }
                                  ? spatialConfig.LightSecondsPerUoMinute
                                  : DefaultSecondsPerUoMinute;

        if (!DateTime.TryParse(spatialConfig?.LightWorldStartUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedWorldStart))
        {
            _worldStartUtc = DefaultWorldStartUtc;

            return;
        }

        _worldStartUtc = parsedWorldStart.ToUniversalTime();
    }

    [ScriptFunction("describe", "Returns the broad ModernUO-style time-of-day description for a mobile.")]
    public string Describe(uint mobileSerial, string? utcNowIso = null)
    {
        if (!TryResolveMobile((Serial)mobileSerial, out var mobile))
        {
            return string.Empty;
        }

        var (hour, _) = ComputeHourMinute(mobile!, utcNowIso);

        return DescribeHour(hour);
    }

    [ScriptFunction("exact_time", "Returns the exact in-game time for a mobile using ModernUO clock rules.")]
    public string ExactTime(uint mobileSerial, string? utcNowIso = null)
    {
        if (!TryResolveMobile((Serial)mobileSerial, out var mobile))
        {
            return string.Empty;
        }

        var (hour, minute) = ComputeHourMinute(mobile!, utcNowIso);
        var normalizedHour = hour % 12;

        if (normalizedHour == 0)
        {
            normalizedHour = 12;
        }

        return $"{normalizedHour}:{minute:D2}";
    }

    private (int Hour, int Minute) ComputeHourMinute(UOMobileEntity mobile, string? utcNowIso)
    {
        var now = ParseUtcNowOrDefault(utcNowIso);
        var totalMinutes = (int)((now - _worldStartUtc).TotalSeconds / _secondsPerUoMinute);
        totalMinutes += mobile.MapId * 320;
        totalMinutes += mobile.Location.X / 16;
        var normalizedMinutes = (totalMinutes % (24 * 60) + 24 * 60) % (24 * 60);

        return (normalizedMinutes / 60, normalizedMinutes % 60);
    }

    private static string DescribeHour(int hour)
        => hour switch
        {
            >= 20 => "It's late at night.",
            >= 16 => "It's early in the evening.",
            >= 13 => "It's the afternoon.",
            >= 12 => "It's around noon.",
            >= 8  => "It's late in the morning.",
            >= 4  => "It's early in the morning.",
            >= 1  => "It's the middle of the night.",
            _     => "'Tis the witching hour."
        };

    private DateTime ParseUtcNowOrDefault(string? utcNowIso)
    {
        if (string.IsNullOrWhiteSpace(utcNowIso) ||
            !DateTime.TryParse(utcNowIso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return DateTime.UtcNow;
        }

        return parsed.ToUniversalTime();
    }

    private bool TryResolveMobile(Serial mobileId, out UOMobileEntity? mobile)
    {
        mobile = null;

        if (mobileId == Serial.Zero)
        {
            return false;
        }

        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            mobile = session.Character;

            return true;
        }

        if (_spatialWorldService is not null &&
            MobileScriptResolver.TryResolveMobile(_spatialWorldService, (uint)mobileId.Value, out mobile))
        {
            return true;
        }

        return false;
    }
}
