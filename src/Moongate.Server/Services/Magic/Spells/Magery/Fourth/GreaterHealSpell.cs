using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.Fourth;

/// <summary>
/// Restores a large amount of hit points to the caster or an allied target.
/// </summary>
public sealed class GreaterHealSpell : MagerySpellBase
{
    private const int GreaterHealSpellId = 29;
    private const int HealBaseAmount = 20;
    private const int HealRandomAmount = 25;

    public override int SpellId => GreaterHealSpellId;

    public override SpellCircleType Circle => SpellCircleType.Fourth;

    public override SpellTargetingType Targeting => SpellTargetingType.OptionalMobile;

    public override SpellInfo Info { get; } = new(
        "Greater Heal",
        "In Vas Mani",
        [ReagentType.Garlic, ReagentType.Ginseng, ReagentType.MandrakeRoot, ReagentType.SpidersSilk],
        [1, 1, 1, 1]
    );

    public override double MinSkill => 30.0;

    public override double MaxSkill => 60.0;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var recipient = target ?? caster;

        if (!recipient.IsAlive)
        {
            return;
        }

        var amount = HealBaseAmount + Random.Shared.Next(HealRandomAmount);
        recipient.Hits = Math.Min(recipient.Hits + amount, recipient.MaxHits);
    }
}
