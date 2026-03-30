using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.Second;

/// <summary>
/// Increases dexterity on a living recipient.
/// </summary>
public sealed class AgilitySpell : MagerySpellBase
{
    private const int AgilitySpellId = 9;
    private const int AgilityDexterityBonus = 10;
    private const string AgilityMarkerKey = "spell_buff_dex";

    public override int SpellId => AgilitySpellId;

    public override SpellCircleType Circle => SpellCircleType.Second;

    public override SpellTargetingType Targeting => SpellTargetingType.OptionalMobile;

    public override SpellInfo Info { get; } = new(
        "Agility",
        "Ex Uus",
        [ReagentType.Bloodmoss, ReagentType.MandrakeRoot],
        [1, 1]
    );

    public override double MinSkill => 10.0;

    public override double MaxSkill => 60.0;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var recipient = target ?? caster;

        if (!recipient.IsAlive)
        {
            return;
        }

        recipient.ApplyRuntimeModifier(new() { DexterityBonus = AgilityDexterityBonus });
        recipient.SetCustomBoolean(AgilityMarkerKey, true);
    }
}
