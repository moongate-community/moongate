using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Magic Arrow (In Por Ylem) deals a small amount of direct damage.
/// </summary>
public sealed class MagicArrowSpell : MagerySpellBase
{
    private const int DamageBase = 3;
    private const int DamageRandom = 5;

    public override int SpellId => SpellIds.Magery.First.MagicArrow;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Magic Arrow",
        "In Por Ylem",
        [ReagentType.SulfurousAsh],
        [1]
    );

    public override double MinSkill => 0.0;

    public override double MaxSkill => 40.0;

    protected override ushort? DefaultEffectItemId => EffectsUtils.Fireball;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (target is null || !target.IsAlive)
        {
            return;
        }

        var damage = DamageBase + Random.Shared.Next(DamageRandom);
        target.Hits = Math.Max(0, target.Hits - damage);
        target.IsAlive = target.Hits > 0;
    }
}
