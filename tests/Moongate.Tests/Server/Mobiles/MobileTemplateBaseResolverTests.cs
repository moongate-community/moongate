using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;
using Moongate.UO.Data.Types;

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

    // Omitting Gender and writing Gender: Male used to be indistinguishable, so a derived template
    // could never state that it is male against a female base.
    [Fact]
    public void Resolve_DerivedForcingMale_BeatsAFemaleBase()
    {
        var baseTemplate = new MobileTemplate { Id = "base_maid", Gender = MobileTemplateGenderType.Female };
        var derived = new MobileTemplate
        {
            Id = "butler", BaseMobile = "base_maid", Gender = MobileTemplateGenderType.Male
        };

        var resolved = new MobileTemplateBaseResolver().Resolve([baseTemplate, derived]);

        Assert.Equal(MobileTemplateGenderType.Male, resolved.Single(template => template.Id == "butler").Gender);
    }

    [Fact]
    public void Resolve_DerivedWithItsOwnNamePool_KeepsIt()
    {
        var baseTemplate = new MobileTemplate { Id = "base_human", NamePool = "male" };
        var derived = new MobileTemplate { Id = "guard_female", BaseMobile = "base_human", NamePool = "female" };

        var resolved = new MobileTemplateBaseResolver().Resolve([baseTemplate, derived]);

        Assert.Equal("female", resolved.Single(template => template.Id == "guard_female").NamePool);
    }

    [Fact]
    public void Resolve_DerivedWithNoGender_StillInheritsTheBase()
    {
        var baseTemplate = new MobileTemplate { Id = "base_maid", Gender = MobileTemplateGenderType.Female };
        var derived = new MobileTemplate { Id = "maid", BaseMobile = "base_maid" };

        var resolved = new MobileTemplateBaseResolver().Resolve([baseTemplate, derived]);

        Assert.Equal(MobileTemplateGenderType.Female, resolved.Single(template => template.Id == "maid").Gender);
    }

    // The five-line data change hangs off this: base_human_npc declares the pool once and all
    // thirteen human templates inherit it.
    [Fact]
    public void Resolve_DerivedWithNoNamePool_InheritsTheBase()
    {
        var baseTemplate = new MobileTemplate { Id = "base_human", NamePool = "male" };
        var derived = new MobileTemplate { Id = "guard", BaseMobile = "base_human" };

        var resolved = new MobileTemplateBaseResolver().Resolve([baseTemplate, derived]);

        Assert.Equal("male", resolved.Single(template => template.Id == "guard").NamePool);
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
