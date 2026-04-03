using Moongate.Server.Data.Magic;
using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Data.Magic;

[TestFixture]
public sealed class SpellInfoTests
{
    [Test]
    public void Constructor_SetsAllProperties()
    {
        var reagents = new[] { ReagentType.Ginseng, ReagentType.Garlic };
        var amounts = new[] { 1, 1 };

        var info = new SpellInfo("Heal", "In Mani", reagents, amounts);

        Assert.Multiple(() =>
        {
            Assert.That(info.Name, Is.EqualTo("Heal"));
            Assert.That(info.Mantra, Is.EqualTo("In Mani"));
            Assert.That(info.Reagents, Is.EqualTo(reagents));
            Assert.That(info.ReagentAmounts, Is.EqualTo(amounts));
        });
    }

    [Test]
    public void Constructor_WithNoReagents_IsValid()
    {
        var info = new SpellInfo("Night Sight", "In Lor", [], []);

        Assert.That(info.Reagents, Is.Empty);
        Assert.That(info.ReagentAmounts, Is.Empty);
    }
}
