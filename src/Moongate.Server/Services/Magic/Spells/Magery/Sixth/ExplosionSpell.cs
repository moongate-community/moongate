using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.Sixth;

/// <summary>
/// Explosion (Vas Ort Flam) applies delayed single-target damage.
/// </summary>
public sealed class ExplosionSpell : MagerySpellBase
{
    private const int DamageBase = 23;
    private const int DamageRandom = 22;
    private static readonly TimeSpan ExplosionDelay = TimeSpan.FromSeconds(3);

    public override int SpellId => SpellIds.Magery.Sixth.Explosion;

    public override SpellCircleType Circle => SpellCircleType.Sixth;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Explosion",
        "Vas Ort Flam",
        [ReagentType.Bloodmoss, ReagentType.MandrakeRoot],
        [1, 1]
    );

    public override double MinSkill => 50.0;

    public override double MaxSkill => 90.0;

    public override ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var target = context.TargetMobile;

        if (target is null || !target.IsAlive)
        {
            return ValueTask.CompletedTask;
        }

        var timerName = $"spell_explosion_{context.Caster.Id}_{target.Id}_{Environment.TickCount64}";

        context.TimerService.RegisterTimer(
            timerName,
            ExplosionDelay,
            () =>
            {
                if (!target.IsAlive)
                {
                    return;
                }

                var damage = DamageBase + Random.Shared.Next(DamageRandom);
                target.Hits = Math.Max(0, target.Hits - damage);
                target.IsAlive = target.Hits > 0;

                context.GameEventBusService.PublishAsync(
                                           new MobilePlayEffectEvent(
                                               target.Id,
                                               target.MapId,
                                               target.Location,
                                               EffectsUtils.Explosion
                                           )
                                       )
                                       .AsTask()
                                       .GetAwaiter()
                                       .GetResult();
                context.GameEventBusService.PublishAsync(
                                           new MobilePlaySoundEvent(
                                               target.Id,
                                               target.MapId,
                                               target.Location,
                                               0x307
                                           )
                                       )
                                       .AsTask()
                                       .GetAwaiter()
                                       .GetResult();
            }
        );

        return ValueTask.CompletedTask;
    }
}
