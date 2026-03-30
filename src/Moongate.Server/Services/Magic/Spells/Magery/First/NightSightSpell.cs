using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.First;

/// <summary>
/// Night Sight (In Lor Xen) marks the recipient so downstream systems can treat them as magically illuminated.
/// </summary>
public sealed class NightSightSpell : MagerySpellBase
{
    private const string NightSightMarkerKey = "magic.night_sight";

    public override int SpellId => SpellIds.Magery.First.NightSight;

    public override SpellCircleType Circle => SpellCircleType.First;

    public override SpellInfo Info { get; } = new(
        "Night Sight",
        "In Lor Xen",
        [ReagentType.SpidersSilk],
        [1]
    );

    public override double MinSkill => 0.0;

    public override double MaxSkill => 30.0;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var recipient = target ?? caster;

        if (!recipient.IsAlive)
        {
            return;
        }

        recipient.SetCustomBoolean(NightSightMarkerKey, true);
    }
}
