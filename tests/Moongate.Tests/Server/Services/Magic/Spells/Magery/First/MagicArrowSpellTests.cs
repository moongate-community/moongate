using Moongate.Server.Services.Magic.Spells.Magery.First;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class MagicArrowSpellTests
{
    [Test]
    public void ApplyEffect_WhenTargetAlive_DealsExpectedDamageRange()
    {
        var spell = new MagicArrowSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true
        };
        var target = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true,
            Hits = 20
        };

        spell.ApplyEffect(caster, target);

        Assert.That(target.Hits, Is.InRange(13, 17));
    }

    [Test]
    public void ApplyEffect_NeverDropsHitsBelowZero()
    {
        var spell = new MagicArrowSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true
        };
        var target = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true,
            Hits = 2
        };

        spell.ApplyEffect(caster, target);

        Assert.That(target.Hits, Is.EqualTo(0));
    }
}
