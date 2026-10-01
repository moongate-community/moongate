using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Internal.Seasons;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The seasons, as the other emulators send them: per map (ModernUO), per region (UOX3), with the light and weather
///     sent again after each change (POL, Sphere). A <c>season_check</c> timer every minute follows the rotation.
/// </summary>
public sealed class SeasonService : ISeasonService
{
    public const string CheckTimerName = "season_check";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly SeasonType[] Rotation = [SeasonType.Spring, SeasonType.Summer, SeasonType.Fall, SeasonType.Winter];

    private readonly ILogger _logger = Log.ForContext<SeasonService>();
    private readonly ConcurrentDictionary<Serial, SeasonListener> _listeners = new();
    private readonly ConcurrentDictionary<MapType, SeasonType> _overrides = new();
    private readonly IDataLoaderService _data;
    private readonly IClockService _clock;
    private readonly WorldConfig _world;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly ITimerService _timers;
    private readonly IMoongateEventBus _events;
    private readonly IGameLoopService _loop;
    private readonly ILightService _light;
    private readonly IWeatherService _weather;

    private IDisposable? _logins;
    private string? _timerId;

    public SeasonService(
        IDataLoaderService data,
        IClockService clock,
        WorldConfig world,
        ISessionService sessions,
        IPacketSendService sender,
        ITimerService timers,
        IMoongateEventBus events,
        IGameLoopService loop,
        ILightService light,
        IWeatherService weather
    )
    {
        _data = data;
        _clock = clock;
        _world = world;
        _sessions = sessions;
        _sender = sender;
        _timers = timers;
        _events = events;
        _loop = loop;
        _light = light;
        _weather = weather;
    }

    public Task StartAsync()
    {
        // The event comes from the login handler's thread; the listeners change on the game loop only.
        _logins = _events.Subscribe<CharacterEnteredWorldEvent>(
            async (evt, cancellationToken) =>
            {
                var work = new LoopActionWorkItem(() => LoggedIn(evt.Character));
                await _loop.PostAsync(work, cancellationToken);
                await work.Completion;
            }
        );
        _timerId = _timers.RegisterTimer(CheckTimerName, CheckInterval, Check, CheckInterval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _logins?.Dispose();
        _logins = null;

        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public void RegionChanged(MobileEntity player, RegionContent? previous, RegionContent? current)
    {
        var listener = _listeners.GetOrAdd(player.Id, _ => new() { Player = player });

        // A relogin brings a new object for the same character: follow the new one.
        if (!ReferenceEquals(listener.Player, player))
        {
            listener = _listeners[player.Id] = new() { Player = player };
        }

        listener.Region = current;
        Send(listener);
    }

    public void Left(Serial player)
    {
        _listeners.TryRemove(player, out _);
    }

    public SeasonType SeasonOf(MobileEntity player)
    {
        return _listeners.TryGetValue(player.Id, out var listener) && ReferenceEquals(listener.Player, player)
            ? SeasonOf(listener)
            : SeasonOf(player.Map);
    }

    public SeasonType SeasonOf(MapType map)
    {
        if (_overrides.TryGetValue(map, out var season))
        {
            return season;
        }

        var configured = _data.GetEntities<MapContent>().FirstOrDefault(content => content.Map == map)?.Season ?? SeasonType.Summer;

        var start = Array.IndexOf(Rotation, configured);

        // Desolation is no season of the year: such a map never rotates.
        if (!_world.SeasonRotation || start < 0)
        {
            return configured;
        }

        // Floored, so a clock before the world start still counts backwards through the seasons.
        var turns = (long)Math.Floor((double)_clock.GetDay(map) / _world.DaysPerSeason);

        return Rotation[(int)(((start + turns) % Rotation.Length + Rotation.Length) % Rotation.Length)];
    }

    public SeasonType SeasonOnLogin(MobileEntity character)
    {
        var season = SeasonOf(character);

        if (_listeners.TryGetValue(character.Id, out var listener) && ReferenceEquals(listener.Player, character))
        {
            listener.LastSent = season;
        }

        return season;
    }

    public SeasonType? OverrideOf(MapType map)
    {
        return _overrides.TryGetValue(map, out var season) ? season : null;
    }

    public void SetOverride(MapType map, SeasonType? season)
    {
        if (season is { } value)
        {
            _overrides[map] = value;
        }
        else
        {
            _overrides.TryRemove(map, out _);
        }

        foreach (var listener in _listeners.Values.Where(listener => listener.Player.Map == map))
        {
            Send(listener);
        }
    }

    private void LoggedIn(MobileEntity character)
    {
        // A player that left before its login completed is not followed any more.
        if (!_sessions.TryGetByCharacterId(character.Id, out var session) ||
            !_listeners.TryGetValue(character.Id, out var listener) ||
            !ReferenceEquals(listener.Player, character))
        {
            return;
        }

        listener.SessionId = session.SessionId;
        Send(listener);
    }

    // A timer callback that throws closes the timer wheel.
    private void Check()
    {
        try
        {
            foreach (var listener in _listeners.Values)
            {
                Send(listener);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Sending the seasons failed");
        }
    }

    private SeasonType SeasonOf(SeasonListener listener)
    {
        return listener.Region?.Season ?? SeasonOf(listener.Player.Map);
    }

    private void Send(SeasonListener listener)
    {
        var season = SeasonOf(listener);

        if (listener.SessionId is not { } sessionId || listener.LastSent == season)
        {
            return;
        }

        if (!_sender.TrySend(sessionId, new SeasonChangePacket(season, true)))
        {
            return;
        }

        listener.LastSent = season;

        // POL and Sphere: the client resets its light and stops its weather on a season packet.
        _sender.TrySend(sessionId, new GlobalLightLevelPacket(_light.LevelFor(listener.Player)));
        _weather.Resend(listener.Player);
    }
}
