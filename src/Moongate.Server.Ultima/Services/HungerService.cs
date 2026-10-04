using System.Runtime.CompilerServices;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Makes the players hungry: one repeating <c>hunger</c> timer looks at the players in the world every minute and
///     takes a point from each one whose <c>ultima.regeneration.hunger_minutes</c> are over, as ModernUO's food
///     decay; each player has its own wait, started when it is first seen, so entering the world just before a check
///     costs nothing. The staff is left alone, as in UOX3. A player is told when it gets hungry and when it starves.
///     Thirst drops with it, at the same pace and with its own two texts. With <c>hunger_enabled</c> or
///     <c>thirst_enabled</c> off that one does not drop; with both off nothing runs.
/// </summary>
public sealed class HungerService : IHungerService, IMoongateStartupService
{
    public const string TimerName = "hunger";
    public const int Full = 20;
    public const int Hungry = 5;
    public const int HungryMessage = 30123;
    public const int StarvingMessage = 30124;
    public const int ThirstyMessage = 30128;
    public const int ParchedMessage = 30129;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger = Log.ForContext<HungerService>();
    private readonly ITimerService _timers;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly RegenerationConfig _config;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    // The wait of each player, gone with its character: it starts again when the character enters the world.
    private readonly ConditionalWeakTable<MobileEntity, HungerClock> _clocks = new();

    private string? _timerId;

    public HungerService(
        ITimerService timers,
        ISessionService sessions,
        IMobileService mobiles,
        ISpeechService speech,
        RegenerationConfig config,
        TimeProvider time,
        ILocalizationService? localization = null
    )
    {
        _time = time;
        _timers = timers;
        _sessions = sessions;
        _mobiles = mobiles;
        _speech = speech;
        _config = config;
        _localization = localization;
    }

    public Task StartAsync()
    {
        if (_config.HungerEnabled || _config.ThirstEnabled)
        {
            _timerId = _timers.RegisterTimer(TimerName, CheckInterval, Decay, CheckInterval, true);
        }

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

    /// <summary>
    ///     Keeps a hunger, or a thirst, from 0 to <see cref="Full" />.
    /// </summary>
    public static int Clamp(int hunger)
    {
        return Math.Clamp(hunger, 0, Full);
    }

    public void Set(MobileEntity mobile, int hunger)
    {
        mobile.Hunger = Clamp(hunger);
    }

    public void SetThirst(MobileEntity mobile, int thirst)
    {
        mobile.Thirst = Clamp(thirst);
    }

    private void Hunger(MobileEntity character)
    {
        if (character.Hunger <= 0)
        {
            return;
        }

        character.Hunger--;

        if (character.Hunger == Hungry)
        {
            _speech.Tell(character, _localization.Text(HungryMessage, "You are hungry."));
        }
        else if (character.Hunger == 0)
        {
            _speech.Tell(
                character,
                _localization.Text(StarvingMessage, "You are starving: your wounds will not heal until you eat.")
            );
        }
    }

    private void Thirst(MobileEntity character)
    {
        if (character.Thirst <= 0)
        {
            return;
        }

        character.Thirst--;

        if (character.Thirst == Hungry)
        {
            _speech.Tell(character, _localization.Text(ThirstyMessage, "You are thirsty."));
        }
        else if (character.Thirst == 0)
        {
            _speech.Tell(
                character,
                _localization.Text(
                    ParchedMessage,
                    "You are parched: your stamina will not come back until you drink."
                )
            );
        }
    }

    // A timer callback that throws closes the timer wheel: one bad character must not stop the server.
    private void Decay()
    {
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var wait = (long)TimeSpan.FromMinutes(_config.HungerMinutes).TotalMilliseconds;

        foreach (var session in _sessions.GetAll())
        {
            try
            {
                if (session.AccountType >= AccountType.GameMaster ||
                    !session.CharacterId.IsValid ||
                    !_mobiles.TryGet(session.CharacterId, out var character))
                {
                    continue;
                }

                var clock = _clocks.GetOrCreateValue(character);

                if (clock.NextAt == 0)
                {
                    clock.NextAt = now + wait;

                    continue;
                }

                if (now < clock.NextAt)
                {
                    continue;
                }

                // One point a check, whatever the gap: a server that paused does not starve its players.
                clock.NextAt = now + wait;

                if (_config.HungerEnabled)
                {
                    Hunger(character);
                }

                if (_config.ThirstEnabled)
                {
                    Thirst(character);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The hunger of {Character} failed", session.CharacterId);
            }
        }
    }
}
