using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Makes the players hungry: one repeating <c>hunger</c> timer, every
///     <c>ultima.regeneration.hunger_minutes</c>, takes a point from every player in the world, as ModernUO's food
///     decay; the staff is left alone, as in UOX3. A player is told when it gets hungry and when it starves. With
///     <c>hunger_enabled</c> off nothing runs.
/// </summary>
public sealed class HungerService : IHungerService, IMoongateStartupService
{
    public const string TimerName = "hunger";
    public const int Full = 20;
    public const int Hungry = 5;
    public const int HungryMessage = 30123;
    public const int StarvingMessage = 30124;

    private readonly ILogger _logger = Log.ForContext<HungerService>();
    private readonly ITimerService _timers;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly RegenerationConfig _config;
    private readonly ILocalizationService? _localization;

    private string? _timerId;

    public HungerService(
        ITimerService timers,
        ISessionService sessions,
        IMobileService mobiles,
        ISpeechService speech,
        RegenerationConfig config,
        ILocalizationService? localization = null
    )
    {
        _timers = timers;
        _sessions = sessions;
        _mobiles = mobiles;
        _speech = speech;
        _config = config;
        _localization = localization;
    }

    public Task StartAsync()
    {
        if (_config.HungerEnabled)
        {
            var interval = TimeSpan.FromMinutes(_config.HungerMinutes);
            _timerId = _timers.RegisterTimer(TimerName, interval, Decay, interval, true);
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

    public void Set(MobileEntity mobile, int hunger)
    {
        mobile.Hunger = Math.Clamp(hunger, 0, Full);
    }

    // A timer callback that throws closes the timer wheel: one bad character must not stop the server.
    private void Decay()
    {
        foreach (var session in _sessions.GetAll())
        {
            try
            {
                if (session.AccountType >= AccountType.GameMaster ||
                    !session.CharacterId.IsValid ||
                    !_mobiles.TryGet(session.CharacterId, out var character) ||
                    character.Hunger <= 0)
                {
                    continue;
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
            catch (Exception exception)
            {
                _logger.Error(exception, "The hunger of {Character} failed", session.CharacterId);
            }
        }
    }
}
