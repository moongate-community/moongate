using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Base;

/// <summary>
/// Base contract adapter for concrete spell implementations.
/// </summary>
public abstract class SpellBase : ISpell
{
    public abstract int SpellId { get; }

    public abstract SpellbookType SpellbookType { get; }

    public abstract SpellInfo Info { get; }

    public abstract int ManaCost { get; }

    public abstract TimeSpan CastDelay { get; }

    public virtual SpellTargetingType Targeting => SpellTargetingType.None;

    public abstract double MinSkill { get; }

    public abstract double MaxSkill { get; }

    protected virtual ushort? DefaultEffectItemId => null;

    protected virtual ushort? DefaultSoundModel => null;

    public virtual void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        _ = caster;
        _ = target;
    }

    public virtual async ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var effectMobile = ResolveDefaultEffectMobile(context);
        var shouldPlayDefaultEffect = effectMobile is not null &&
                                      ShouldPlayDefaultEffect(context, effectMobile);

        ApplyEffect(context.Caster, context.TargetMobile);

        if (!shouldPlayDefaultEffect || effectMobile is null)
        {
            return;
        }

        if (DefaultEffectItemId is ushort effectItemId)
        {
            await context.GameEventBusService.PublishAsync(
                new MobilePlayEffectEvent(
                    effectMobile.Id,
                    effectMobile.MapId,
                    effectMobile.Location,
                    effectItemId
                ),
                cancellationToken
            );
        }

        if (DefaultSoundModel is ushort soundModel)
        {
            await context.GameEventBusService.PublishAsync(
                new MobilePlaySoundEvent(
                    effectMobile.Id,
                    effectMobile.MapId,
                    effectMobile.Location,
                    soundModel
                ),
                cancellationToken
            );
        }

        return;
    }

    protected virtual UOMobileEntity? ResolveDefaultEffectMobile(SpellExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.TargetMobile ?? context.Caster;
    }

    protected virtual bool ShouldPlayDefaultEffect(SpellExecutionContext context, UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mobile);

        return mobile.IsAlive;
    }
}
