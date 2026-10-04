using System.Runtime.CompilerServices;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Mobiles;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Regenerates hit points, mana and stamina, as ModernUO's classic rules: a point at a time, hit points every 11
///     seconds, stamina every 7, mana from 7 seconds down to half a second by intelligence and Meditation. One
///     repeating <c>regeneration</c> timer every second ticks the players in the world; the NPCs are ticked by their
///     think. A prop of the mobile (<c>regen.hits</c>, <c>regen.mana</c>, <c>regen.stamina</c>, in seconds) replaces
///     the rate.
/// </summary>
public sealed class RegenerationService : IRegenerationService, IMoongateStartupService
{
    public const string TimerName = "regeneration";
    public const string HitsProp = "regen.hits";
    public const string ManaProp = "regen.mana";
    public const string StaminaProp = "regen.stamina";

    private const double FastestSeconds = 0.5;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger = Log.ForContext<RegenerationService>();
    private readonly IMobileStateService _state;
    private readonly RegenerationConfig _config;
    private readonly TimeProvider _time;
    private readonly ITimerService? _timers;
    private readonly ISessionService? _sessions;
    private readonly IMobileService? _mobiles;

    // The waits of each mobile, gone with it.
    private readonly ConditionalWeakTable<MobileEntity, RegenerationClock> _clocks = new();

    private string? _timerId;

    public RegenerationService(
        IMobileStateService state,
        RegenerationConfig config,
        TimeProvider time,
        ITimerService? timers = null,
        ISessionService? sessions = null,
        IMobileService? mobiles = null
    )
    {
        _state = state;
        _config = config;
        _time = time;
        _timers = timers;
        _sessions = sessions;
        _mobiles = mobiles;
    }

    public Task StartAsync()
    {
        _timerId = _timers?.RegisterTimer(TimerName, CheckInterval, TickPlayers, CheckInterval, true);

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers?.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public void Tick(MobileEntity mobile)
    {
        var clock = _clocks.GetOrCreateValue(mobile);
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var change = new MobileStatsChange();
        var changed = false;

        // A player with an empty stomach gets no hit points back, as in UOX3.
        var starving = _config.HungerEnabled && !mobile.IsNpc && mobile.Hunger <= 0;

        if (starving)
        {
            clock.HitsAt = 0;
        }
        else
        {
            if (Due(mobile.Hits, mobile.HitsMax, clock.HitsAt, now, Seconds(mobile, HitsProp, _config.HitsSeconds), out var hitsAt))
            {
                change.Hits = mobile.Hits + 1;
                changed = true;
            }

            clock.HitsAt = hitsAt;
        }

        if (Due(mobile.Mana, mobile.ManaMax, clock.ManaAt, now, Seconds(mobile, ManaProp, ManaSeconds(mobile)), out var manaAt))
        {
            change.Mana = mobile.Mana + 1;
            changed = true;
        }

        clock.ManaAt = manaAt;

        if (Due(mobile.Stamina, mobile.StaminaMax, clock.StaminaAt, now, Seconds(mobile, StaminaProp, _config.StaminaSeconds), out var staminaAt))
        {
            change.Stamina = mobile.Stamina + 1;
            changed = true;
        }

        clock.StaminaAt = staminaAt;

        if (changed)
        {
            _state.SetStats(mobile, change);
        }
    }

    public double ManaSeconds(MobileEntity mobile)
    {
        // ModernUO's rule before AOS, on half of intelligence plus Meditation.
        var meditation = mobile.Skills.FirstOrDefault(skill => skill.Skill == SkillType.Meditation)?.Base / 10.0 ?? 0;
        var points = (mobile.Intelligence + meditation) * 0.5;
        var slowest = _config.ManaSeconds;
        double seconds;

        if (points <= 0)
        {
            seconds = slowest;
        }
        else if (points <= 100)
        {
            seconds = slowest - 239 * points / 2400 + 19 * points * points / 48000;
        }
        else if (points < 120)
        {
            seconds = 1.0;
        }
        else
        {
            seconds = 0.75;
        }

        return Math.Clamp(seconds, FastestSeconds, Math.Max(FastestSeconds, slowest));
    }

    // Whether a point of the bar is due now, and when the next one is: 0 for a full bar.
    private static bool Due(int current, int maximum, long at, long now, double seconds, out long next)
    {
        if (current >= maximum)
        {
            next = 0;

            return false;
        }

        var wait = (long)(seconds * 1000);

        if (at == 0)
        {
            next = now + wait;

            return false;
        }

        if (now < at)
        {
            next = at;

            return false;
        }

        next = now + wait;

        return true;
    }

    // The mobile's own rate, when a script gave it one, else the given seconds.
    private static double Seconds(MobileEntity mobile, string prop, double seconds)
    {
        return mobile.Props?.GetValueOrDefault(prop) switch
        {
            long own when own > 0     => own,
            double own when own > 0.0 => Math.Max(own, 0.1),
            _                         => seconds
        };
    }

    // A timer callback that throws closes the timer wheel: one bad mobile must not stop the server.
    private void TickPlayers()
    {
        if (_sessions is null || _mobiles is null)
        {
            return;
        }

        foreach (var session in _sessions.GetAll())
        {
            try
            {
                if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var character))
                {
                    Tick(character);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The regeneration of {Character} failed", session.CharacterId);
            }
        }
    }
}
