using Moongate.Server.Services.Magic;
using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class ReagentCatalogTests
{
    [Test]
    public void GetTemplateId_BlackPearl_ReturnsExpectedTemplateId()
    {
        Assert.That(ReagentCatalog.GetTemplateId(ReagentType.BlackPearl), Is.EqualTo("black_pearl"));
    }

    [Test]
    public void GetTemplateId_AllMageryReagents_ReturnNonEmptyTemplateIds()
    {
        var mageryReagents = new[]
        {
            ReagentType.BlackPearl,
            ReagentType.Bloodmoss,
            ReagentType.Garlic,
            ReagentType.Ginseng,
            ReagentType.MandrakeRoot,
            ReagentType.Nightshade,
            ReagentType.SulfurousAsh,
            ReagentType.SpidersSilk
        };

        foreach (var reagent in mageryReagents)
        {
            Assert.That(
                ReagentCatalog.GetTemplateId(reagent),
                Is.Not.Null.And.Not.Empty,
                $"{reagent} should map to a non-empty template id"
            );
        }
    }

    [Test]
    public void GetTemplateId_None_ReturnsNull()
    {
        Assert.That(ReagentCatalog.GetTemplateId(ReagentType.None), Is.Null);
    }
}
