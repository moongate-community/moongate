using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     ModernUO's light cycle: one repeating <c>light_cycle</c> timer every 5 seconds sends each player in the world the
///     light of its time of day when it differs from the last one sent.
/// </summary>
public sealed class LightService : ILightService
{
    public const string TimerName = "light_cycle";
    private const int NoOverride = -1;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger _logger = Log.ForContext<LightService>();
    private readonly ConcurrentDictionary<Serial, int> _sent = new();
    private readonly IClockService _clock;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly ITimerService _timers;
    private readonly IGameLoopService _loop;
    private readonly WorldConfig _world;

    private string? _timerId;
    private volatile int _override = NoOverride;

    public int? Override => _override is var level && level != NoOverride ? level : null;

    public LightService(
        IClockService clock,
        ISessionService sessions,
        IMobileService mobiles,
        IPacketSendService sender,
        ITimerService timers,
        IGameLoopService loop,
        WorldConfig world
    )
    {
        _clock = clock;
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
        _timers = timers;
        _loop = loop;
        _world = world;
    }

    public Task StartAsync()
    {
        _timerId = _timers.RegisterTimer(TimerName, CheckInterval, Check, CheckInterval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public int LevelFor(MobileEntity mobile)
    {
        if (Override is { } level)
        {
            return level;
        }

        var time = _clock.GetTime(mobile.Map, mobile.Location.X);
        var day = _world.DayLight;
        var night = _world.NightLight;

        // ModernUO's bands: two hours of fade on each side of the night.
        return time.Hours switch
        {
            < 4 => night,
            < 6 => night + ((time.Hours - 4) * 60 + time.Minutes) * (day - night) / 120,
            < 22 => day,
            _ => day + ((time.Hours - 22) * 60 + time.Minutes) * (night - day) / 120
        };
    }

    public int LevelOnLogin(MobileEntity character)
    {
        var level = LevelFor(character);
        _sent[character.Id] = level;

        return level;
    }

    public async Task SetOverrideAsync(int? level, CancellationToken cancellationToken = default)
    {
        var work = new LoopActionWorkItem(
            () =>
            {
                _override = level ?? NoOverride;
                Send();
            }
        );
        await _loop.PostAsync(work, cancellationToken);
        await work.Completion;
    }

    // A timer callback that throws closes the timer wheel.
    private void Check()
    {
        try
        {
            Send();
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The light cycle failed");
        }
    }

    private void Send()
    {
        var seen = new HashSet<Serial>();

        foreach (var session in _sessions.GetAll())
        {
            if (!session.CharacterId.IsValid ||
                !_mobiles.IsInWorld(session.CharacterId) ||
                !_mobiles.TryGet(session.CharacterId, out var character))
            {
                continue;
            }

            seen.Add(character.Id);
            var level = LevelFor(character);

            if (_sent.TryGetValue(character.Id, out var last) && last == level)
            {
                continue;
            }

            if (_sender.TrySend(session.SessionId, new GlobalLightLevelPacket(level)))
            {
                _sent[character.Id] = level;
            }
        }

        // The characters that left the world are sent their level again at their next login.
        foreach (var gone in _sent.Keys.Where(serial => !seen.Contains(serial)))
        {
            _sent.TryRemove(gone, out _);
        }
    }
}
