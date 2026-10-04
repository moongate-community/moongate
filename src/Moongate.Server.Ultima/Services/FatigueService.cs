using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The stamina a step costs, with ModernUO's numbers: overloaded, 5 and one more every 25 stones over the maximum,
///     twice running; running, a point every 16 steps and one a step below a tenth of the stamina.
///     <c>ultima.regeneration.fatigue_enabled</c> turns it off.
/// </summary>
public sealed class FatigueService : IFatigueService
{
    public const int OverloadedMessage = 500109;
    public const int FatiguedMessage = 500110;
    public const int RunSteps = 16;
    public const int OverloadedWarning = 30133;

    private const int OverloadedLoss = 5;
    private const int StonesPerLoss = 25;

    private readonly IWeightService _weight;
    private readonly IMobileStateService _state;
    private readonly ISpeechService _speech;
    private readonly RegenerationConfig _config;
    private readonly ILocalizationService? _localization;

    public FatigueService(
        IWeightService weight,
        IMobileStateService state,
        ISpeechService speech,
        RegenerationConfig config,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _weight = weight;
        _state = state;
        _speech = speech;
        _config = config;
    }

    public bool CanStep(GameSession session, MobileEntity mobile, bool running)
    {
        if (!Applies(session, mobile) || mobile.Stamina > 0)
        {
            return true;
        }

        if (Overweight(mobile) > 0)
        {
            _speech.TellCliloc(mobile, OverloadedMessage);

            return false;
        }

        if (running)
        {
            _speech.TellCliloc(mobile, FatiguedMessage);

            return false;
        }

        return true;
    }

    public void Stepped(GameSession session, MobileEntity mobile, bool running)
    {
        if (!Applies(session, mobile))
        {
            return;
        }

        var loss = 0;
        var overweight = Overweight(mobile);

        if (overweight > 0)
        {
            loss = OverloadedLoss + overweight / StonesPerLoss;

            if (running)
            {
                loss *= 2;
            }
        }

        if (running)
        {
            // Nearly spent, every running step costs.
            if ((mobile.Stamina - loss) * 10 < mobile.StaminaMax)
            {
                loss++;
            }

            var state = session.Get(MovementSessionKeys.State);

            if (state is not null && ++state.RunSteps > RunSteps)
            {
                state.RunSteps = 0;
                loss++;
            }
        }

        if (loss > 0 && mobile.Stamina > 0)
        {
            _state.SetStats(mobile, new MobileStatsChange { Stamina = Math.Max(mobile.Stamina - loss, 0) });
        }
    }

    public void LoadChanged(GameSession session, MobileEntity mobile, bool warn)
    {
        _state.SendStatus(session, mobile);

        if (!warn || !Applies(session, mobile))
        {
            return;
        }

        var carried = _weight.Carried(mobile);
        var maximum = _weight.MaxCarried(mobile);

        if (carried > maximum)
        {
            _speech.Tell(
                mobile,
                _localization.Text(OverloadedWarning, "You are overloaded: you carry {0} stones of {1}.", carried, maximum)
            );
        }
    }

    private bool Applies(GameSession session, MobileEntity mobile)
    {
        return _config.FatigueEnabled && !mobile.IsNpc && session.AccountType < AccountType.GameMaster;
    }

    private int Overweight(MobileEntity mobile)
    {
        return _weight.Carried(mobile) - _weight.MaxCarried(mobile);
    }
}
