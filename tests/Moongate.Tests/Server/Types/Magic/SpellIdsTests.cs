using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Types.Magic;

[TestFixture]
public sealed class SpellIdsTests
{
    [Test]
    public void MageryFirst_ContainsCanonicalFirstCircleIds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellIds.Magery.First.Clumsy, Is.EqualTo(1));
            Assert.That(SpellIds.Magery.First.CreateFood, Is.EqualTo(2));
            Assert.That(SpellIds.Magery.First.Feeblemind, Is.EqualTo(3));
            Assert.That(SpellIds.Magery.First.Heal, Is.EqualTo(4));
            Assert.That(SpellIds.Magery.First.MagicArrow, Is.EqualTo(5));
            Assert.That(SpellIds.Magery.First.NightSight, Is.EqualTo(6));
            Assert.That(SpellIds.Magery.First.ReactiveArmor, Is.EqualTo(7));
            Assert.That(SpellIds.Magery.First.Weaken, Is.EqualTo(8));
        });
    }
}
