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
///     twice running; running, a point every 16 steps and one a step below a tenth of the stamina. A step that would
///     take the last of the stamina is refused. <c>ultima.regeneration.fatigue_enabled</c> turns it off.
/// </summary>
public sealed class FatigueService : IFatigueService
{
    public const int OverloadedMessage = 500109;
    public const int FatiguedMessage = 500110;
    public const int RunSteps = 16;
    public const int OverloadedWarning = 30133;

    private const int OverloadedLossBase = 5;
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
        if (!Applies(session, mobile))
        {
            return true;
        }

        var overloaded = OverloadedLoss(mobile, running);

        // As ModernUO: the step that would take the last of the stamina is not taken, so a point that came back
        // does not buy a tile.
        if (overloaded > 0 && mobile.Stamina - overloaded <= 0)
        {
            _speech.TellCliloc(mobile, OverloadedMessage);

            return false;
        }

        if (running && mobile.Stamina - overloaded - SpentLoss(mobile, overloaded) <= 0)
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

        var loss = OverloadedLoss(mobile, running);

        if (running)
        {
            loss += SpentLoss(mobile, loss);

            var state = session.Get(MovementSessionKeys.State);

            if (state is not null && ++state.RunSteps >= RunSteps)
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
        return _config.FatigueEnabled && !mobile.IsNpc && !mobile.IsDead && session.AccountType < AccountType.GameMaster;
    }

    // What a step costs a mobile that carries more than it may; nothing at the maximum or below.
    private int OverloadedLoss(MobileEntity mobile, bool running)
    {
        var overweight = _weight.Carried(mobile) - _weight.MaxCarried(mobile);

        if (overweight <= 0)
        {
            return 0;
        }

        var loss = OverloadedLossBase + overweight / StonesPerLoss;

        return running ? loss * 2 : loss;
    }

    // Nearly spent, every running step costs a point: below a tenth of the stamina, once the other loss is taken.
    private static int SpentLoss(MobileEntity mobile, int loss)
    {
        return (mobile.Stamina - loss) * 10 < mobile.EffectiveStaminaMax ? 1 : 0;
    }
}
