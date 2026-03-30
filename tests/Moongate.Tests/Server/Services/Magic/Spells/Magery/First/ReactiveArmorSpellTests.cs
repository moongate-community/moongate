using Moongate.Server.Services.Magic.Spells.Magery.First;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class ReactiveArmorSpellTests
{
    [Test]
    public void ApplyEffect_WhenTargetIsNull_IncreasesCasterPhysicalResistance()
    {
        var spell = new ReactiveArmorSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true,
            BaseResistances = new()
            {
                Physical = 12
            }
        };

        spell.ApplyEffect(caster, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.RuntimeModifiers, Is.Not.Null);
                Assert.That(caster.RuntimeModifiers!.PhysicalResist, Is.EqualTo(5));
                Assert.That(caster.EffectivePhysicalResistance, Is.EqualTo(17));
                Assert.That(caster.TryGetCustomBoolean("magic.reactive_armor", out var isMarked), Is.True);
                Assert.That(isMarked, Is.True);
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenRecipientIsDead_DoesNotApplyModifier()
    {
        var spell = new ReactiveArmorSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true
        };
        var target = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = false,
            BaseResistances = new()
            {
                Physical = 12
            }
        };

        spell.ApplyEffect(caster, target);

        Assert.Multiple(
            () =>
            {
                Assert.That(target.RuntimeModifiers, Is.Null);
                Assert.That(target.EffectivePhysicalResistance, Is.EqualTo(12));
                Assert.That(target.TryGetCustomBoolean("magic.reactive_armor", out _), Is.False);
            }
        );
    }
}
