using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Types.Magic;

[TestFixture]
public sealed class ReagentTypeTests
{
    [Test]
    public void ReagentType_None_HasValueZero()
    {
        Assert.That((int)ReagentType.None, Is.EqualTo(0));
    }

    [Test]
    public void ReagentType_AllEightMageryReagents_AreDefined()
    {
        var names = Enum.GetNames<ReagentType>();

        Assert.That(names, Does.Contain(nameof(ReagentType.BlackPearl)));
        Assert.That(names, Does.Contain(nameof(ReagentType.Bloodmoss)));
        Assert.That(names, Does.Contain(nameof(ReagentType.Garlic)));
        Assert.That(names, Does.Contain(nameof(ReagentType.Ginseng)));
        Assert.That(names, Does.Contain(nameof(ReagentType.MandrakeRoot)));
        Assert.That(names, Does.Contain(nameof(ReagentType.Nightshade)));
        Assert.That(names, Does.Contain(nameof(ReagentType.SulfurousAsh)));
        Assert.That(names, Does.Contain(nameof(ReagentType.SpidersSilk)));
    }
}
