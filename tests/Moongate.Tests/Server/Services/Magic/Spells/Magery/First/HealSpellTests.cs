using Moongate.Server.Services.Magic.Spells.Magery.First;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class HealSpellTests
{
    [Test]
    public void ApplyEffect_WhenTargetIsNull_HealsCasterWithinExpectedRange()
    {
        var spell = new HealSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true,
            Hits = 20,
            MaxHits = 100
        };

        spell.ApplyEffect(caster, null);

        Assert.That(caster.Hits, Is.InRange(30, 44));
    }

    [Test]
    public void ApplyEffect_ClampsHealingToMaxHits()
    {
        var spell = new HealSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true,
            Hits = 95,
            MaxHits = 100
        };

        spell.ApplyEffect(caster, caster);

        Assert.That(caster.Hits, Is.EqualTo(100));
    }
}
