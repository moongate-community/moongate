using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Internal.Weather;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Weather;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     UOX3's region weather: one state per profile, rolled on the <c>weather_hour</c> timer (a game hour, 60 game
///     minutes) with a new temperature every 24 of them; the <c>weather_check</c> timer every 5 seconds sends each player
///     what changed, such as walking into a building (a static more than 10 above its head is a roof), and thunders
///     during storms. A player is sent nothing before its login completes.
/// </summary>
public sealed class WeatherService : IWeatherService
{
    public const string HourTimerName = "weather_hour";
    public const string CheckTimerName = "weather_check";
    private const string NoWeather = "none";
    private const int RoofHeight = 10;
    private const int ThunderOneIn = 4;
    private const int FirstThunder = 0x28;

    // The client drops the weather a few minutes after the last 0x65: it is resent every minute and every game hour.
    private const int ChecksPerResend = 12;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly WeatherState Dry = new(WeatherKindType.None, 0, 20);

    private readonly ILogger _logger = Log.ForContext<WeatherService>();
    private readonly ConcurrentDictionary<Serial, WeatherViewer> _viewers = new();
    private readonly ConcurrentDictionary<string, WeatherState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _dayTemperatures = new(StringComparer.Ordinal);
    private readonly Lock _rolling = new();
    private readonly IDataLoaderService _data;
    private readonly IMapService _map;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ITimerService _timers;
    private readonly IMoongateEventBus _events;
    private readonly IGameLoopService _loop;
    private readonly WorldConfig _world;
    private readonly Random _random;

    private string? _hourTimer;
    private string? _checkTimer;
    private IDisposable? _logins;
    private int _hours;
    private int _checks;

    public WeatherService(
        IDataLoaderService data,
        IMapService map,
        ISessionService sessions,
        IPacketSendService sender,
        ITimerService timers,
        IMoongateEventBus events,
        IGameLoopService loop,
        WorldConfig world,
        Random? random = null
    )
    {
        _data = data;
        _map = map;
        _sessions = sessions;
        _sender = sender;
        _timers = timers;
        _events = events;
        _loop = loop;
        _world = world;
        _random = random ?? Random.Shared;
    }

    public Task StartAsync()
    {
        RollDay();
        RollHour();
        var hour = TimeSpan.FromSeconds(60 * _world.SecondsPerUoMinute);
        _hourTimer = _timers.RegisterTimer(HourTimerName, hour, OnHour, hour, true);
        _checkTimer = _timers.RegisterTimer(CheckTimerName, CheckInterval, Check, CheckInterval, true);
        // The event comes from the login handler's thread; the viewers change on the game loop only.
        _logins = _events.Subscribe<CharacterEnteredWorldEvent>(
            async (evt, cancellationToken) =>
            {
                var work = new LoopActionWorkItem(() => LoggedIn(evt.Character));
                await _loop.PostAsync(work, cancellationToken);
                await work.Completion;
            }
        );

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        foreach (var id in new[] { _hourTimer, _checkTimer }.OfType<string>())
        {
            _timers.UnregisterTimer(id);
        }

        _hourTimer = _checkTimer = null;
        _logins?.Dispose();
        _logins = null;

        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        var viewer = _viewers.GetOrAdd(player.Id, _ => new() { Player = player });

        // A relogin brings a new object for the same character: follow the new one.
        if (!ReferenceEquals(viewer.Player, player))
        {
            viewer = _viewers[player.Id] = new() { Player = player };
        }

        viewer.Region = current;
        Update(viewer, false);
    }

    public void Left(Serial player)
    {
        _viewers.TryRemove(player, out _);
    }

    public string ProfileOf(MobileEntity player)
    {
        if (_viewers.TryGetValue(player.Id, out var viewer) && viewer.Region is { } region)
        {
            return region.Weather;
        }

        return _data.GetEntities<MapContent>().FirstOrDefault(map => map.Map == player.Map)?.Weather ?? NoWeather;
    }

    public WeatherState StateOf(string profile)
    {
        return _states.GetValueOrDefault(profile) ?? Dry;
    }

    public void Force(string profile, WeatherKindType kind)
    {
        lock (_rolling)
        {
            _states[profile] = StateOf(profile) with
            {
                Kind = kind,
                Density = kind == WeatherKindType.None ? 0 : WeatherRolls.MaxDensity
            };
        }
    }

    private void LoggedIn(MobileEntity character)
    {
        // A player that left before its login completed is not followed any more.
        if (!_sessions.TryGetByCharacterId(character.Id, out var session) ||
            !_viewers.TryGetValue(character.Id, out var viewer) ||
            !ReferenceEquals(viewer.Player, character))
        {
            return;
        }

        viewer.SessionId = session.SessionId;
        viewer.LastSent = null;
        Update(viewer, false);
    }

    // A timer callback that throws closes the timer wheel.
    private void OnHour()
    {
        try
        {
            if (++_hours % 24 == 0)
            {
                RollDay();
            }

            RollHour();
            ResendAll();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Rolling the weather failed");
        }
    }

    private void Check()
    {
        try
        {
            if (++_checks % ChecksPerResend == 0)
            {
                ResendAll();
            }

            foreach (var viewer in _viewers.Values)
            {
                Update(viewer, true);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Sending the weather failed");
        }
    }

    private void Update(WeatherViewer viewer, bool mayThunder)
    {
        if (viewer.SessionId is not { } sessionId)
        {
            return;
        }

        var player = viewer.Player;
        var indoors = IsIndoors(player);
        var state = indoors ? Dry with { Temperature = StateOf(ProfileOf(player)).Temperature } : StateOf(ProfileOf(player));
        var packet = new WeatherPacket(state.Kind, state.Density, state.Temperature);
        var last = viewer.LastSent;

        if (last is null || last.Kind != packet.Kind || last.Density != packet.Density || last.Temperature != packet.Temperature)
        {
            if (_sender.TrySend(sessionId, packet))
            {
                viewer.LastSent = packet;
            }
        }

        if (mayThunder && !indoors && state.Kind == WeatherKindType.Storm && _random.Next(0, ThunderOneIn) == 0)
        {
            _sender.TrySend(sessionId, new PlaySoundPacket(FirstThunder + _random.Next(0, 2), player.Location));
        }
    }

    private void ResendAll()
    {
        foreach (var viewer in _viewers.Values)
        {
            viewer.LastSent = null;
        }
    }

    // UOX3's roof check, on the map's statics.
    private bool IsIndoors(MobileEntity player)
    {
        try
        {
            foreach (var tile in _map.GetStatics(player.Map, player.Location.X, player.Location.Y))
            {
                if (tile.Z > player.Location.Z + RoofHeight)
                {
                    return true;
                }
            }
        }
        catch (Exception exception) when (exception is KeyNotFoundException or ArgumentOutOfRangeException)
        {
            // A map that is not loaded, or a spot outside it: no roof.
        }

        return false;
    }

    private void RollDay()
    {
        lock (_rolling)
        {
            foreach (var profile in _data.GetEntities<WeatherContent>())
            {
                _dayTemperatures[profile.Name] = WeatherRolls.DayTemperature(profile, _random);
            }
        }
    }

    private void RollHour()
    {
        lock (_rolling)
        {
            foreach (var profile in _data.GetEntities<WeatherContent>())
            {
                _states[profile.Name] = WeatherRolls.Hour(profile, _dayTemperatures.GetValueOrDefault(profile.Name), _random);
            }
        }
    }
}
