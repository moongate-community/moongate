using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Gumps;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps who attacked whom, asks a dead player which of them to report, counts the reports and forgets the counts
///     with time: one repeating <c>murder-decay</c> timer every five minutes goes through the players in the world.
/// </summary>
public sealed class MurderService : IMurderService, IMoongateStartupService
{
    public const string DecayTimerName = "murder-decay";
    public const string AskTimerName = "murder-ask";
    public const string ReportGump = "report_murder";
    public const string YesClick = "yes";

    /// <summary>
    ///     The client's "You have been reported for murder!"
    /// </summary>
    public const int ReportedCliloc = 1049067;

    /// <summary>
    ///     The client's "You are now known as a murderer!"
    /// </summary>
    public const int MurdererCliloc = 502134;

    /// <summary>
    ///     The karma lost for each kill, as ModernUO.
    /// </summary>
    public const int KarmaPerKill = -1000;

    private static readonly TimeSpan DecayInterval = TimeSpan.FromMinutes(5);

    private readonly ILogger _logger = Log.ForContext<MurderService>();
    private readonly ITimerService _timers;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly IWorldViewService _view;
    private readonly ISpeechService _speech;
    private readonly IGumpTemplateService _gumps;
    private readonly ICrimeService _crimes;
    private readonly MurderConfig _config;
    private readonly TimeProvider _time;

    // Who attacked each victim as a criminal, and when. Game loop only.
    private readonly Dictionary<Serial, Dictionary<Serial, DateTimeOffset>> _aggressors = [];

    // Who a victim reported, and when: it cannot report the same killer twice in a row.
    private readonly Dictionary<(Serial Victim, Serial Killer), DateTimeOffset> _reported = [];

    private string? _timerId;

    public MurderService(
        ITimerService timers,
        ISessionService sessions,
        IMobileService mobiles,
        IMobileStateService state,
        IWorldViewService view,
        ISpeechService speech,
        IGumpTemplateService gumps,
        ICrimeService crimes,
        MurderConfig config,
        TimeProvider time
    )
    {
        _timers = timers;
        _sessions = sessions;
        _mobiles = mobiles;
        _state = state;
        _view = view;
        _speech = speech;
        _gumps = gumps;
        _crimes = crimes;
        _config = config;
        _time = time;
    }

    public Task StartAsync()
    {
        _timerId = _timers.RegisterTimer(DecayTimerName, DecayInterval, DecayOnline, DecayInterval, true);

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

    public void Aggressed(MobileEntity attacker, MobileEntity victim)
    {
        if (attacker.IsNpc || victim.IsNpc || attacker.Id == victim.Id)
        {
            return;
        }

        if (!_aggressors.TryGetValue(victim.Id, out var attackers))
        {
            _aggressors[victim.Id] = attackers = [];
        }

        attackers[attacker.Id] = _time.GetUtcNow();
    }

    public void Struck(MobileEntity attacker, MobileEntity victim)
    {
        if (_aggressors.TryGetValue(victim.Id, out var attackers) && attackers.ContainsKey(attacker.Id))
        {
            attackers[attacker.Id] = _time.GetUtcNow();
        }
    }

    public void Died(MobileEntity victim)
    {
        if (!_aggressors.Remove(victim.Id, out var attackers))
        {
            return;
        }

        var since = _time.GetUtcNow().AddSeconds(-_config.AggressorSeconds);
        var queue = new Queue<Serial>(attackers.Where(pair => pair.Value >= since).Select(pair => pair.Key));

        if (queue.Count > 0)
        {
            _timers.RegisterTimer(AskTimerName, TimeSpan.FromSeconds(_config.ReportDelaySeconds), () => AskSafely(victim, queue));
        }
    }

    public bool Report(MobileEntity victim, MobileEntity killer)
    {
        var now = _time.GetUtcNow();

        if (killer.IsNpc || !_mobiles.IsInWorld(killer.Id))
        {
            return false;
        }

        if (_reported.TryGetValue((victim.Id, killer.Id), out var when) &&
            now < when.AddMinutes(_config.RecentlyReportedMinutes))
        {
            return false;
        }

        _reported[(victim.Id, killer.Id)] = now;
        var wasMurderer = killer.IsMurderer;
        killer.Kills++;
        killer.ShortTermMurders++;
        killer.KillsDecayAt = now.UtcDateTime.AddHours(_config.LongTermHours);
        killer.ShortTermDecayAt = now.UtcDateTime.AddHours(_config.ShortTermHours);
        _state.SetStats(killer, new MobileStatsChange { Karma = killer.Kills * KarmaPerKill });
        _speech.TellCliloc(killer, ReportedCliloc);

        if (killer.IsMurderer && !wasMurderer)
        {
            _speech.TellCliloc(killer, MurdererCliloc);
            _view.MobileFlagsChanged(killer);
        }

        _logger.Information("{Killer:l} ({Serial:l}) was reported for murder by {Victim:l}: {Kills} kills", killer.Name, killer.Id, victim.Name, killer.Kills);

        return true;
    }

    public void Looted(MobileEntity looter, ItemEntity corpse)
    {
        if (looter.IsNpc ||
            corpse.ItemId != CorpseProps.Graphic ||
            !corpse.TryGetProp<long>(CorpseProps.Owner, out var owner) ||
            owner == looter.Id.Value ||
            !corpse.GetProp(CorpseProps.Innocent, false) ||
            (_sessions.TryGetByCharacterId(looter.Id, out var session) && session.AccountType >= AccountType.GameMaster))
        {
            return;
        }

        _crimes.MakeCriminal(looter);
    }

    public void Restore(MobileEntity mobile)
    {
        Decay(mobile, _time.GetUtcNow().UtcDateTime);
    }

    // A timer callback that throws closes the timer wheel: the gump of a player must not stop the server.
    private void AskSafely(MobileEntity victim, Queue<Serial> queue)
    {
        try
        {
            Ask(victim, queue);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Asking {Victim:l} to report a murder failed", victim.Name);
        }
    }

    // One question at a time, as ModernUO's gump: the answer, or closing it, brings the next. The killer is looked up
    // again when the answer comes: it may have left the world and come back as another entity meanwhile.
    private void Ask(MobileEntity victim, Queue<Serial> queue)
    {
        while (queue.TryDequeue(out var serial))
        {
            if (!_mobiles.TryGet(serial, out var killer) ||
                !_mobiles.IsInWorld(killer.Id) ||
                !_sessions.TryGetByCharacterId(victim.Id, out var session))
            {
                continue;
            }

            var opened = _gumps.Open(
                session,
                ReportGump,
                new Dictionary<string, string> { ["name"] = killer.Name ?? "" },
                (_, answer) =>
                {
                    if (answer.Click == YesClick && _mobiles.TryGet(serial, out var live))
                    {
                        Report(victim, live);
                    }

                    Ask(victim, queue);
                },
                // Another report gump opened on the victim closed this one: it brings its own question.
                (_, reason) =>
                {
                    if (reason != GumpCloseReasonType.Replaced)
                    {
                        Ask(victim, queue);
                    }
                }
            );

            if (opened)
            {
                return;
            }
        }
    }

    // A timer callback that throws closes the timer wheel: one bad mobile must not stop the server.
    private void DecayOnline()
    {
        try
        {
            var now = _time.GetUtcNow().UtcDateTime;
            Prune(_time.GetUtcNow());

            foreach (var session in _sessions.GetAll())
            {
                if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var character))
                {
                    Decay(character, now);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The murder decay failed");
        }
    }

    // What is old enough no longer matters: the attacks that can no longer be reported and the reports that can be made again.
    private void Prune(DateTimeOffset now)
    {
        var attackSince = now.AddSeconds(-_config.AggressorSeconds);

        foreach (var (victim, attackers) in _aggressors.ToArray())
        {
            foreach (var attacker in attackers.Where(pair => pair.Value < attackSince).Select(pair => pair.Key).ToArray())
            {
                attackers.Remove(attacker);
            }

            if (attackers.Count == 0)
            {
                _aggressors.Remove(victim);
            }
        }

        var reportedSince = now.AddMinutes(-_config.RecentlyReportedMinutes);

        foreach (var key in _reported.Where(pair => pair.Value < reportedSince).Select(pair => pair.Key).ToArray())
        {
            _reported.Remove(key);
        }
    }

    private void Decay(MobileEntity mobile, DateTime now)
    {
        var wasMurderer = mobile.IsMurderer;

        // Counts that came without a time, such as one a staff gave, start theirs now.
        if (mobile.Kills > 0 && mobile.KillsDecayAt is null)
        {
            mobile.KillsDecayAt = now.AddHours(_config.LongTermHours);
        }

        if (mobile.ShortTermMurders > 0 && mobile.ShortTermDecayAt is null)
        {
            mobile.ShortTermDecayAt = now.AddHours(_config.ShortTermHours);
        }

        while (mobile.Kills > 0 && mobile.KillsDecayAt is { } killsAt && killsAt <= now)
        {
            mobile.Kills--;
            mobile.KillsDecayAt = mobile.Kills > 0 ? killsAt.AddHours(_config.LongTermHours) : null;
        }

        while (mobile.ShortTermMurders > 0 && mobile.ShortTermDecayAt is { } shortAt && shortAt <= now)
        {
            mobile.ShortTermMurders--;
            mobile.ShortTermDecayAt = mobile.ShortTermMurders > 0 ? shortAt.AddHours(_config.ShortTermHours) : null;
        }

        if (wasMurderer && !mobile.IsMurderer && _mobiles.IsInWorld(mobile.Id))
        {
            _view.MobileFlagsChanged(mobile);
        }
    }
}
