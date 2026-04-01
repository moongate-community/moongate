using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Feeblemind (Rel Wis) weakens the target's intelligence.
/// </summary>
public sealed class FeeblemindSpell : MagerySpellBase
{
    private const int IntelligencePenalty = -10;
    private const int MarkerValue = 1;
    private const string MarkerKey = "magic.feeblemind";

    public override int SpellId => SpellIds.Magery.First.Feeblemind;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Feeblemind",
        "Rel Wis",
        [ReagentType.Nightshade, ReagentType.Ginseng],
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
            IntelligenceBonus = IntelligencePenalty
        });
        target.SetCustomInteger(MarkerKey, MarkerValue);
    }
}
