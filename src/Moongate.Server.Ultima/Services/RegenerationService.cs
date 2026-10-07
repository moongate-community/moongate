using System.Runtime.CompilerServices;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
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
///     seconds, stamina every 7, mana from 7 seconds down to three quarters of one by intelligence and Meditation. One
///     repeating <c>regeneration</c> timer every second ticks the players in the world; the NPCs are ticked by their
///     think. A prop of the mobile ( <c>regen.hits</c>, <c>regen.mana</c>, <c>regen.stamina</c>, in seconds) replaces
///     the rate.
/// </summary>
public sealed class RegenerationService : IRegenerationService, IMoongateStartupService
{
    public const string TimerName = "regeneration";
    public const string HitsProp = "regen.hits";
    public const string ManaProp = "regen.mana";
    public const string StaminaProp = "regen.stamina";

    private const double FastestSeconds = 0.5;
    private const double SlowestSeconds = 3600;
    private const double ClassicManaSeconds = 7.0;
    private const int MaximumPointsPerTick = 5;

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

        // The dead get nothing back, as ModernUO: only a resurrection does.
        if (mobile.IsDead)
        {
            clock.HitsAt = clock.ManaAt = clock.StaminaAt = 0;

            return;
        }

        // Nothing to give back: the three waits start again when a bar drops.
        if (mobile.Hits >= mobile.HitsMax && mobile.Mana >= mobile.ManaMax && mobile.Stamina >= mobile.StaminaMax)
        {
            clock.HitsAt = clock.ManaAt = clock.StaminaAt = 0;

            return;
        }

        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var change = new MobileStatsChange();
        var changed = false;

        // A player with an empty stomach gets no hit points back, as in UOX3; the staff, which never gets hungry,
        // is left alone.
        if (_config.HungerEnabled && !mobile.IsNpc && mobile.Hunger <= 0 && !IsStaff(mobile))
        {
            clock.HitsAt = 0;
        }
        else
        {
            var hits = Due(
                mobile.Hits,
                mobile.HitsMax,
                clock.HitsAt,
                now,
                Seconds(mobile, HitsProp, _config.HitsSeconds),
                out var hitsAt
            );
            clock.HitsAt = hitsAt;

            if (hits > 0)
            {
                change.Hits = mobile.Hits + hits;
                changed = true;
            }
        }

        var mana = Due(
            mobile.Mana,
            mobile.ManaMax,
            clock.ManaAt,
            now,
            Seconds(mobile, ManaProp, ManaSeconds(mobile)),
            out var manaAt
        );
        clock.ManaAt = manaAt;

        if (mana > 0)
        {
            change.Mana = mobile.Mana + mana;
            changed = true;
        }

        // A parched player gets no stamina back, as in UOX3; the staff is left alone here too.
        if (_config.ThirstEnabled && !mobile.IsNpc && mobile.Thirst <= 0 && !IsStaff(mobile))
        {
            clock.StaminaAt = 0;
        }
        else
        {
            var stamina = Due(
                mobile.Stamina,
                mobile.StaminaMax,
                clock.StaminaAt,
                now,
                Seconds(mobile, StaminaProp, _config.StaminaSeconds),
                out var staminaAt
            );
            clock.StaminaAt = staminaAt;

            if (stamina > 0)
            {
                change.Stamina = mobile.Stamina + stamina;
                changed = true;
            }
        }

        if (changed)
        {
            _state.SetStats(mobile, change);
        }
    }

    public double ManaSeconds(MobileEntity mobile)
    {
        // ModernUO's rule before AOS, on half of intelligence plus Meditation: seven seconds down to three quarters
        // of one, half a second at the least. Another slowest rate keeps the shape of the curve.
        var meditation = mobile.Skills.FirstOrDefault(skill => skill.Skill == SkillType.Meditation)?.Base / 10.0 ?? 0;
        var points = (mobile.Intelligence + meditation) * 0.5;
        double seconds;

        if (points <= 0)
        {
            seconds = ClassicManaSeconds;
        }
        else if (points <= 100)
        {
            seconds = ClassicManaSeconds - 239 * points / 2400 + 19 * points * points / 48000;
        }
        else if (points < 120)
        {
            seconds = 1.0;
        }
        else
        {
            seconds = 0.75;
        }

        return Math.Clamp(seconds, FastestSeconds, ClassicManaSeconds) * _config.ManaSeconds / ClassicManaSeconds;
    }

    // How many points of the bar are due now, and when the next one is: 0 for a full bar. A tick that comes a little
    // early or late does not move the rate: the next point is counted from when this one was due. After a long gap,
    // as an NPC that slept, a few points come at once and the wait starts again.
    private static int Due(int current, int maximum, long at, long now, double seconds, out long next)
    {
        if (current >= maximum)
        {
            next = 0;

            return 0;
        }

        var wait = Math.Max(1, (long)(seconds * 1000));

        if (at == 0)
        {
            next = now + wait;

            return 0;
        }

        if (now < at)
        {
            next = at;

            return 0;
        }

        var due = 1 + (now - at) / wait;

        if (due > MaximumPointsPerTick)
        {
            next = now + wait;

            return Math.Min(MaximumPointsPerTick, maximum - current);
        }

        next = at + due * wait;

        return (int)Math.Min(due, maximum - current);
    }

    // The mobile's own rate, when a script gave it one, else the given seconds; kept within what the configuration
    // allows.
    private static double Seconds(MobileEntity mobile, string prop, double seconds)
    {
        return mobile.Props?.GetValueOrDefault(prop) switch
        {
            long own when own > 0                             => Math.Min(own, SlowestSeconds),
            int own when own > 0                              => Math.Min(own, SlowestSeconds),
            double own when double.IsFinite(own) && own > 0.0 => Math.Clamp(own, 0.1, SlowestSeconds),
            _                                                 => seconds
        };
    }

    private bool IsStaff(MobileEntity mobile)
    {
        return _sessions is not null &&
               _sessions.TryGetByCharacterId(mobile.Id, out var session) &&
               session.AccountType >= AccountType.GameMaster;
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
