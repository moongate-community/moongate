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

    public virtual void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        _ = caster;
        _ = target;
    }

    public virtual ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ApplyEffect(context.Caster, context.TargetMobile);

        return ValueTask.CompletedTask;
    }
}
