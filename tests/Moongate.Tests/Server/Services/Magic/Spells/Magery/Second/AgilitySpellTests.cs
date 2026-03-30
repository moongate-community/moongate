using Moongate.Server.Services.Magic.Spells.Magery.Second;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Second;

[TestFixture]
public sealed class AgilitySpellTests
{
    [Test]
    public void Info_UsesAgilityMetadata()
    {
        var spell = new AgilitySpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(9));
                Assert.That(spell.Info.Name, Is.EqualTo("Agility"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("Ex Uus"));
                Assert.That(spell.Info.Reagents, Is.EqualTo(new[] { ReagentType.Bloodmoss, ReagentType.MandrakeRoot }));
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1 }));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsNull_BuffsCasterWithDexterityAndMarker()
    {
        var spell = new AgilitySpell();
        var caster = new UOMobileEntity
        {
            Dexterity = 25,
            IsAlive = true
        };

        spell.ApplyEffect(caster, null);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.RuntimeModifiers, Is.Not.Null);
                Assert.That(caster.RuntimeModifiers!.DexterityBonus, Is.EqualTo(10));
                Assert.That(caster.EffectiveDexterity, Is.EqualTo(35));
                Assert.That(caster.TryGetCustomBoolean("spell_buff_dex", out var marker), Is.True);
                Assert.That(marker, Is.True);
            }
        );
    }
}
