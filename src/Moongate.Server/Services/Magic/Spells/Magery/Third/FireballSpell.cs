using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.Third;

/// <summary>
/// Deals direct damage to a single living target.
/// </summary>
public sealed class FireballSpell : MagerySpellBase
{
    private const int DamageBaseAmount = 6;
    private const int DamageRandomAmount = 10;
    private const int FireballSpellId = 20;

    public override int SpellId => FireballSpellId;

    public override SpellCircleType Circle => SpellCircleType.Third;

    public override SpellInfo Info { get; } = new(
        "Fireball",
        "Vas Flam",
        [ReagentType.BlackPearl, ReagentType.SulfurousAsh],
        [1, 1]
    );

    public override double MinSkill => 0;

    public override double MaxSkill => 60;

    public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (target is null || !target.IsAlive)
        {
            return;
        }

        var damage = DamageBaseAmount + Random.Shared.Next(DamageRandomAmount);
        target.Hits = Math.Max(0, target.Hits - damage);
    }
}
