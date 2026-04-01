using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Weaken (Des Mani) weakens the target's strength.
/// </summary>
public sealed class WeakenSpell : MagerySpellBase
{
    private const int StrengthPenalty = -10;
    private const int MarkerValue = 1;
    private const string MarkerKey = "magic.weaken";

    public override int SpellId => SpellIds.Magery.First.Weaken;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Weaken",
        "Des Mani",
        [ReagentType.Garlic, ReagentType.Nightshade],
        [1, 1]
    );

    public override double MinSkill => 0.0;

    public override double MaxSkill => 20.0;

    protected override ushort? DefaultEffectItemId => EffectsUtils.Curse;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (target is null || !target.IsAlive)
        {
            return;
        }

        target.ApplyRuntimeModifier(new()
        {
            StrengthBonus = StrengthPenalty
        });
        target.SetCustomInteger(MarkerKey, MarkerValue);
    }
}
