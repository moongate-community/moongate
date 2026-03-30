using Moongate.Server.Data.Magic;

namespace Moongate.Tests.Server.Data.Magic;

[TestFixture]
public sealed class SpellbookDataTests
{
    [Test]
    public void HasSpell_WhenBookIsEmpty_ReturnsFalse()
    {
        var data = new SpellbookData(0UL);

        Assert.That(data.HasSpell(4), Is.False);
    }

    [Test]
    public void WithSpell_AddsSpellBit()
    {
        var data = new SpellbookData(0UL).WithSpell(4);

        Assert.That(data.HasSpell(4), Is.True);
    }

    [Test]
    public void WithoutSpell_RemovesSpellBit()
    {
        var data = new SpellbookData(0UL).WithSpell(4).WithoutSpell(4);

        Assert.That(data.HasSpell(4), Is.False);
    }

    [Test]
    public void Content_RoundTripsMultipleSpells()
    {
        var data = new SpellbookData(0UL).WithSpell(1).WithSpell(8).WithSpell(64);
        var restored = new SpellbookData(data.Content);

        Assert.Multiple(() =>
        {
            Assert.That(restored.HasSpell(1), Is.True);
            Assert.That(restored.HasSpell(8), Is.True);
            Assert.That(restored.HasSpell(64), Is.True);
        });
    }
}
