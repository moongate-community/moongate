using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Tests.Server.Mobiles;

public class MobileTemplateBaseResolverTests
{
    [Fact]
    public void Resolve_Cycle_Throws()
    {
        var a = new MobileTemplate { Id = "a", BaseMobile = "b" };
        var b = new MobileTemplate { Id = "b", BaseMobile = "a" };

        Assert.Throws<InvalidDataException>(() => new MobileTemplateBaseResolver().Resolve([a, b]));
    }

    [Fact]
    public void Resolve_MergesScalarsSkillsTagsAndAppearance()
    {
        var baseTemplate = new MobileTemplate
        {
            Id = "base_human",
            Name = "Human",
            Strength = 80,
            Dexterity = 70,
            Tags = ["human"],
            Skills = { ["Tactics"] = 500 },
            Appearance = new() { Body = 0x0190, SkinHue = "1002", HairStyle = 10 }
        };

        var derived = new MobileTemplate
        {
            Id = "guard",
            BaseMobile = "base_human",
            Name = "Town Guard",
            Strength = 100,
            Tags = ["guard"],
            Skills = { ["Swordsmanship"] = 900 },
            Appearance = new() { HairStyle = 22 }
        };

        var resolved = new MobileTemplateBaseResolver().Resolve([baseTemplate, derived]);
        var guard = resolved.Single(t => t.Id == "guard");

        Assert.Equal("Town Guard", guard.Name);           // derived string wins
        Assert.Equal(100, guard.Strength);                // derived scalar wins
        Assert.Equal(70, guard.Dexterity);                // inherited (derived left default)
        Assert.Null(guard.BaseMobile);                    // cleared after resolution
        Assert.Equal(["human", "guard"], guard.Tags);     // union
        Assert.Equal(500, guard.Skills["Tactics"]);       // inherited
        Assert.Equal(900, guard.Skills["Swordsmanship"]); // derived
        Assert.Equal(0x0190, guard.Appearance.Body);      // inherited
        Assert.Equal("1002", guard.Appearance.SkinHue);   // inherited
        Assert.Equal(22, guard.Appearance.HairStyle);     // derived overlay
    }

    [Fact]
    public void Resolve_UnknownBase_Throws()
    {
        var derived = new MobileTemplate { Id = "guard", BaseMobile = "missing" };

        var ex = Assert.Throws<InvalidDataException>(() => new MobileTemplateBaseResolver().Resolve([derived]));
        Assert.Contains("missing", ex.Message);
    }
}
