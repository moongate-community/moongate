using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Services.Magic.Base;

[TestFixture]
public sealed class MagerySpellBaseTests
{
    [Test]
    [TestCase(SpellCircleType.First, 4, 0.5)]
    [TestCase(SpellCircleType.Second, 6, 0.75)]
    [TestCase(SpellCircleType.Third, 9, 1.0)]
    [TestCase(SpellCircleType.Eighth, 50, 2.75)]
    public void ManaCostAndDelay_MatchCircleTable(
        SpellCircleType circle,
        int expectedManaCost,
        double expectedDelaySeconds
    )
    {
        var spell = new StubMagerySpell(circle);

        Assert.Multiple(() =>
        {
            Assert.That(spell.ManaCost, Is.EqualTo(expectedManaCost));
            Assert.That(spell.CastDelay.TotalSeconds, Is.EqualTo(expectedDelaySeconds));
        });
    }

    [Test]
    public void ManaCost_WhenCircleIsNone_ThrowsInvalidOperationException()
    {
        var spell = new StubMagerySpell(SpellCircleType.None);

        Assert.Throws<InvalidOperationException>(() => _ = spell.ManaCost);
    }

    private sealed class StubMagerySpell : MagerySpellBase
    {
        public StubMagerySpell(SpellCircleType circle)
        {
            Circle = circle;
        }

        public override int SpellId => 0;

        public override SpellbookType SpellbookType => SpellbookType.Regular;

        public override SpellCircleType Circle { get; }

        public override SpellInfo Info { get; } = new("Test", "An", [], []);

        public override double MinSkill => 0;

        public override double MaxSkill => 60;

        public override void ApplyEffect(UOMobileEntity caster, UOMobileEntity? target)
        {
            _ = caster;
            _ = target;
        }
    }
}
