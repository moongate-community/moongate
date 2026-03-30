using Moongate.Server.Services.Magic.Spells.Magery.Third;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.Third;

[TestFixture]
public sealed class FireballSpellTests
{
    private const int FireballSpellId = 20;

    [Test]
    public void Info_UsesFireballMetadata()
    {
        var spell = new FireballSpell();

        Assert.Multiple(
            () =>
            {
                Assert.That(spell.SpellId, Is.EqualTo(FireballSpellId));
                Assert.That(spell.Info.Name, Is.EqualTo("Fireball"));
                Assert.That(spell.Info.Mantra, Is.EqualTo("Vas Flam"));
                Assert.That(spell.Info.Reagents, Is.EqualTo(new[] { ReagentType.BlackPearl, ReagentType.SulfurousAsh }));
                Assert.That(spell.Info.ReagentAmounts, Is.EqualTo(new[] { 1, 1 }));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsAlive_ReducesTargetHitsUsingHitsProperties()
    {
        var spell = new FireballSpell();
        var caster = new UOMobileEntity
        {
            Hits = 50,
            MaxHits = 50
        };
        var target = new UOMobileEntity
        {
            Hits = 20,
            MaxHits = 20
        };

        spell.ApplyEffect(caster, target);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.Hits, Is.EqualTo(50));
                Assert.That(target.Hits, Is.InRange(5, 14));
            }
        );
    }

    [Test]
    public void ApplyEffect_WhenTargetIsDead_DoesNothing()
    {
        var spell = new FireballSpell();
        var caster = new UOMobileEntity
        {
            Hits = 50,
            MaxHits = 50
        };
        var target = new UOMobileEntity
        {
            Hits = 0,
            MaxHits = 20
        };

        spell.ApplyEffect(caster, target);

        Assert.Multiple(
            () =>
            {
                Assert.That(caster.Hits, Is.EqualTo(50));
                Assert.That(target.Hits, Is.EqualTo(0));
            }
        );
    }
}
