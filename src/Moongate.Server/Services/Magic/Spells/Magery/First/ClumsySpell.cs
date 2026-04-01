using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Clumsy (Uus Jux) weakens the target's dexterity.
/// </summary>
public sealed class ClumsySpell : MagerySpellBase
{
    private const int DexterityPenalty = -10;
    private const int MarkerValue = 1;
    private const string MarkerKey = "magic.clumsy";

    public override int SpellId => SpellIds.Magery.First.Clumsy;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredMobile;

    public override SpellInfo Info { get; } = new(
        "Clumsy",
        "Uus Jux",
        [ReagentType.Bloodmoss, ReagentType.Nightshade],
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
            DexterityBonus = DexterityPenalty
        });
        target.SetCustomInteger(MarkerKey, MarkerValue);
    }
}
