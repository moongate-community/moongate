using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Heal (In Mani) restores a modest amount of hit points.
/// </summary>
public sealed class HealSpell : MagerySpellBase
{
    private const int HealBase = 10;
    private const int HealRandom = 15;

    public override int SpellId => SpellIds.Magery.First.Heal;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellTargetingType Targeting => SpellTargetingType.OptionalMobile;

    public override SpellInfo Info { get; } = new(
        "Heal",
        "In Mani",
        [ReagentType.Ginseng, ReagentType.Garlic],
        [1, 1]
    );

    public override double MinSkill => 0.0;

    public override double MaxSkill => 60.0;

    protected override ushort? DefaultEffectItemId => EffectsUtils.Heal;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var recipient = target ?? caster;

        if (!recipient.IsAlive)
        {
            return;
        }

        var healAmount = HealBase + Random.Shared.Next(HealRandom);
        var maxHits = Math.Max(1, recipient.MaxHits);
        recipient.Hits = Math.Clamp(recipient.Hits + healAmount, 0, maxHits);
    }
}
