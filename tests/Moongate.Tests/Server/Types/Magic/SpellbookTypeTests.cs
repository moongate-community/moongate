using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Types.Magic;

[TestFixture]
public sealed class SpellbookTypeTests
{
    [Test]
    public void SpellbookType_UsesExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellbookType.None, Is.EqualTo((SpellbookType)0));
            Assert.That(SpellbookType.Regular, Is.EqualTo((SpellbookType)1));
            Assert.That(SpellbookType.Necromancer, Is.EqualTo((SpellbookType)2));
            Assert.That(SpellbookType.Paladin, Is.EqualTo((SpellbookType)3));
        });
    }
}
