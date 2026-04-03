using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.Fifth;

/// <summary>
/// Paralyze (An Ex Por) locks a living target in place for a short duration.
/// </summary>
public sealed class ParalyzeSpell : MagerySpellBase
{
    private static readonly TimeSpan ParalyzeDuration = TimeSpan.FromSeconds(7);

    public override int SpellId => SpellIds.Magery.Fifth.Paralyze;

    public override SpellCircleType Circle => SpellCircleType.Fifth;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Paralyze",
        "An Ex Por",
        [ReagentType.Garlic, ReagentType.MandrakeRoot, ReagentType.SpidersSilk],
        [1, 1, 1]
    );

    public override double MinSkill => 50.0;

    public override double MaxSkill => 90.0;

    public override async ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var target = context.TargetMobile;

        if (target is null || !target.IsAlive)
        {
            return;
        }

        var expiresAtUtc = DateTime.UtcNow + ParalyzeDuration;
        var timerName = ParalyzeStateHelper.GetTimerName(target.Id);
        string? timerId = null;

        if (ParalyzeStateHelper.TryGetTimerId(target, out var existingTimerId) &&
            !string.IsNullOrWhiteSpace(existingTimerId))
        {
            context.TimerService.UnregisterTimer(existingTimerId);
        }

        timerId = context.TimerService.RegisterTimer(
            timerName,
            ParalyzeDuration,
            () =>
            {
                if (!ParalyzeStateHelper.TryGetTimerId(target, out var currentTimerId) ||
                    !string.Equals(currentTimerId, timerId, StringComparison.Ordinal))
                {
                    return;
                }

                if (ParalyzeStateHelper.TryGetExpiresAtUtc(target, out var currentExpiresAtUtc) &&
                    currentExpiresAtUtc > DateTime.UtcNow)
                {
                    return;
                }

                ParalyzeStateHelper.Clear(target);
            }
        );
        ParalyzeStateHelper.Apply(target, expiresAtUtc, timerId);

        await context.GameEventBusService.PublishAsync(
            new MobilePlayEffectEvent(target.Id, target.MapId, target.Location, EffectsUtils.Paralyze),
            cancellationToken
        );
        await context.GameEventBusService.PublishAsync(
            new MobilePlaySoundEvent(target.Id, target.MapId, target.Location, 0x204),
            cancellationToken
        );
    }
}
