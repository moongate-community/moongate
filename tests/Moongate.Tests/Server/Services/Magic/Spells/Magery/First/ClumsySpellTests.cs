using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class ClumsySpellTests
{
    [Test]
    public void Info_UsesCanonicalMetadata()
    {
        var spell = new ClumsySpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.Info.Name, Is.EqualTo("Clumsy"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("Uus Jux"));
                Assert.That(spell.Info.Reagents, Is.EqualTo([ReagentType.Bloodmoss, ReagentType.Nightshade]));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsAlive_ShouldApplyDexterityPenaltyAndMarker()
    {
        var spell = new ClumsySpell();
        var caster = new UOMobileEntity
        {
            IsAlive = true
        };
        var target = new UOMobileEntity
        {
            IsAlive = true,
            Dexterity = 50
        };

        spell.ApplyEffect(caster, target);

        Assert.Multiple(
            () =>
            {
                Assert.That(target.EffectiveDexterity, Is.EqualTo(40));
                Assert.That(target.RuntimeModifiers, Is.Not.Null);
                Assert.That(target.RuntimeModifiers!.DexterityBonus, Is.EqualTo(-10));
                Assert.That(target.TryGetCustomInteger("magic.clumsy", out var marker), Is.True);
                Assert.That(marker, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsNull_ShouldLeaveCasterUnchanged()
    {
        var spell = new ClumsySpell();
        var caster = new UOMobileEntity
        {
            IsAlive = true,
            Dexterity = 50
        };

        spell.ApplyEffect(caster, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.EffectiveDexterity, Is.EqualTo(50));
                Assert.That(caster.RuntimeModifiers, Is.Null);
                Assert.That(caster.CustomProperties, Is.Empty);
            }
        );
    }
}
