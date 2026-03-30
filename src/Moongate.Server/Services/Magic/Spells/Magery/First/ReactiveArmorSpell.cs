using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Reactive Armor (Flam Sanct) grants a modest physical resistance bonus and marks the recipient as protected.
/// </summary>
public sealed class ReactiveArmorSpell : MagerySpellBase
{
    private const string ReactiveArmorMarkerKey = "magic.reactive_armor";
    private const int PhysicalResistBonus = 5;

    public override int SpellId => SpellIds.Magery.First.ReactiveArmor;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellInfo Info { get; } = new(
        "Reactive Armor",
        "Flam Sanct",
        [ReagentType.Garlic, ReagentType.SpidersSilk, ReagentType.SulfurousAsh],
        [1, 1, 1]
    );

    public override double MinSkill => 0.0;

    public override double MaxSkill => 35.0;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var recipient = target ?? caster;

        if (!recipient.IsAlive)
        {
            return;
        }

        recipient.ApplyRuntimeModifier(new MobileModifierDelta
        {
            PhysicalResist = PhysicalResistBonus
        });
        recipient.SetCustomBoolean(ReactiveArmorMarkerKey, true);
    }
}
