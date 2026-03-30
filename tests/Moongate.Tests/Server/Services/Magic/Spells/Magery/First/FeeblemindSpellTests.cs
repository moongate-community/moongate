using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class FeeblemindSpellTests
{
    [Test]
    public void ApplyEffect_WhenTargetIsAlive_ShouldApplyIntelligencePenaltyAndMarker()
    {
        var spell = new FeeblemindSpell();
        var caster = new UOMobileEntity
        {
            IsAlive = true
        };
        var target = new UOMobileEntity
        {
            IsAlive = true,
            Intelligence = 50
        };

        spell.ApplyEffect(caster, target);

        Assert.Multiple(
            () =>
            {
                Assert.That(target.EffectiveIntelligence, Is.EqualTo(40));
                Assert.That(target.RuntimeModifiers, Is.Not.Null);
                Assert.That(target.RuntimeModifiers!.IntelligenceBonus, Is.EqualTo(-10));
                Assert.That(target.TryGetCustomInteger("magic.feeblemind", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsNull_ShouldLeaveCasterUnchanged()
    {
        var spell = new FeeblemindSpell();
        var caster = new UOMobileEntity
        {
            IsAlive = true,
            Intelligence = 50
        };

        spell.ApplyEffect(caster, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.EffectiveIntelligence, Is.EqualTo(50));
                Assert.That(caster.RuntimeModifiers, Is.Null);
                Assert.That(caster.CustomProperties, Is.Empty);
            }
        );
    }
}
