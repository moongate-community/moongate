using Moongate.Server.Services.Magic.Spells.Magery.Fourth;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Fourth;

[TestFixture]
public sealed class GreaterHealSpellTests
{
    [Test]
    public void Info_UsesGreaterHealMetadata()
    {
        var spell = new GreaterHealSpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(29));
                Assert.That(spell.Info.Name, Is.EqualTo("Greater Heal"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("In Vas Mani"));
                Assert.That(
                    spell.Info.Reagents,
                    Is.EqualTo(
                        new[] { ReagentType.Garlic, ReagentType.Ginseng, ReagentType.MandrakeRoot, ReagentType.SpidersSilk }
                    )
                );
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1, 1, 1 }));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsNull_HealsCasterUsingHitsProperties()
    {
        var spell = new GreaterHealSpell();
        var caster = new UOMobileEntity
        {
            Hits = 10,
            MaxHits = 100,
            IsAlive = true
        };

        spell.ApplyEffect(caster, null);

        Assert.That(caster.Hits, Is.InRange(30, 54));
    }
}
