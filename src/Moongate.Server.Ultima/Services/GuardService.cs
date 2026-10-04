using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Sends a guard for each criminal near the player that called, as ModernUO's guarded regions: an NPC of
///     <c>ultima.crime.guard_template</c> appears on the criminal with the teleport effect and sound, says its line,
///     and leaves the same way after <c>ultima.crime.guard_seconds</c>. A summoned guard bears the prop
///     <c>guard.summoned</c>: one a stopped server left in the world is removed at the first check.
/// </summary>
public sealed class GuardService : IGuardService, IMoongateStartupService
{
    public const string TimerName = "guards";
    public const string SummonedProp = "guard.summoned";
    public const int CallRange = 14;
    public const int TeleportEffect = 0x3728;
    public const int TeleportSound = 0x1FE;
    public const int LineMessage = 30138;

    private const string Word = "guards";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger = Log.ForContext<GuardService>();
    private readonly ITimerService _timers;
    private readonly INpcService _npcs;
    private readonly IMobileService _mobiles;
    private readonly ISectorService _sectors;
    private readonly IRegionService _regions;
    private readonly ISessionService _sessions;
    private readonly ISpeechService _speech;
    private readonly IEffectService _effects;
    private readonly IGameLoopService _loop;
    private readonly CrimeConfig _config;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    // The criminals a guard was sent for, and the guards in the world with when they leave. Game loop only.
    private readonly HashSet<Serial> _wanted = [];
    private readonly Dictionary<Serial, (Serial Criminal, DateTime LeavesAt)> _guards = [];

    private string? _timerId;
    private bool _adopted;

    public GuardService(
        ITimerService timers,
        INpcService npcs,
        IMobileService mobiles,
        ISectorService sectors,
        IRegionService regions,
        ISessionService sessions,
        ISpeechService speech,
        IEffectService effects,
        IGameLoopService loop,
        CrimeConfig config,
        TimeProvider time,
        ILocalizationService? localization = null
    )
    {
        _timers = timers;
        _npcs = npcs;
        _mobiles = mobiles;
        _sectors = sectors;
        _regions = regions;
        _sessions = sessions;
        _speech = speech;
        _effects = effects;
        _loop = loop;
        _config = config;
        _time = time;
        _localization = localization;
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

    public void Heard(MobileEntity speaker, string text, IReadOnlyList<int>? keywords = null)
    {
        if (keywords?.Contains((int)SpeechKeywordType.Guards) == true || SaysTheWord(text))
        {
            Call(speaker);
        }
    }

    public int Call(MobileEntity caller)
    {
        if (!_config.GuardsEnabled || _regions.Find(caller.Map, caller.Location)?.Guarded != true)
        {
            return 0;
        }

        var sent = 0;

        foreach (var mobile in _sectors.GetMobilesInRange(caller.Map, caller.Location, CallRange))
        {
            if (mobile.Criminal && !IsStaff(mobile) && _mobiles.IsInWorld(mobile.Id) && _wanted.Add(mobile.Id))
            {
                _ = SummonAsync(mobile);
                sent++;
            }
        }

        return sent;
    }

    // The word alone, not inside another: "guards!" calls, "vanguards" does not.
    private static bool SaysTheWord(string text)
    {
        var index = text.IndexOf(Word, StringComparison.OrdinalIgnoreCase);

        while (index >= 0)
        {
            var end = index + Word.Length;

            if ((index == 0 || !char.IsLetter(text[index - 1])) && (end == text.Length || !char.IsLetter(text[end])))
            {
                return true;
            }

            index = text.IndexOf(Word, end, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool IsStaff(MobileEntity mobile)
    {
        return _sessions.TryGetByCharacterId(mobile.Id, out var session) && session.AccountType >= AccountType.GameMaster;
    }

    // The spawn saves the guard first, off the loop: what follows goes back onto it.
    private async Task SummonAsync(MobileEntity criminal)
    {
        try
        {
            var guard = await _npcs.SpawnAsync(
                _config.GuardTemplate,
                criminal.Map,
                criminal.Location,
                new Dictionary<string, object?> { [SummonedProp] = true }
            );

            if (!_loop.TryPost(new LoopActionWorkItem(() => Arrived(guard, criminal))))
            {
                _wanted.Remove(criminal.Id);
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "No guard of {Template} came for {Criminal}", _config.GuardTemplate, criminal.Id);
            _loop.TryPost(new LoopActionWorkItem(() => _wanted.Remove(criminal.Id)));
        }
    }

    private void Arrived(MobileEntity guard, MobileEntity criminal)
    {
        _guards[guard.Id] = (criminal.Id, Now().AddSeconds(_config.GuardSeconds));
        Appear(guard.Map, guard.Location);
        _speech.Say(guard, _localization.Text(LineMessage, "Thou wilt regret thine actions, swine!"));
    }

    private void Appear(MapType map, Point3D location)
    {
        _effects.PlayAt(map, location, new EffectOptions { Graphic = TeleportEffect });
        _speech.PlaySound(map, location, TeleportSound);
    }

    // A timer callback that throws closes the timer wheel: one bad guard must not stop the server.
    private void Check()
    {
        try
        {
            var now = Now();

            if (!_adopted)
            {
                // What a stopped server left: summoned guards with nobody to send them away.
                foreach (var mobile in _mobiles.Mobiles)
                {
                    if (mobile.IsNpc && mobile.GetProp(SummonedProp, false) && !_guards.ContainsKey(mobile.Id))
                    {
                        _guards[mobile.Id] = (default, now);
                    }
                }

                _adopted = true;
            }

            foreach (var (serial, (criminal, leavesAt)) in _guards.ToArray())
            {
                if (leavesAt > now)
                {
                    continue;
                }

                _guards.Remove(serial);
                _wanted.Remove(criminal);

                if (_mobiles.TryGet(serial, out var guard))
                {
                    Appear(guard.Map, guard.Location);
                    _ = RemoveAsync(serial);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "The check of the guards failed");
        }
    }

    private async Task RemoveAsync(Serial guard)
    {
        try
        {
            await _npcs.RemoveAsync(guard);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The guard {Guard} could not be removed", guard);
        }
    }

    private DateTime Now()
    {
        return _time.GetUtcNow().UtcDateTime;
    }
}
