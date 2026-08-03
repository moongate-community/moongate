using Moongate.Http.Plugin.Data.Api.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Mobiles;

/// <summary>
/// What the DTOs owe the caller: every field the template carries, the hue specs left as the strings
/// they are, and an art url built the same way the item summary builds its own.
/// </summary>
public class MobileTemplateResponseTests
{
    [Fact]
    public void Summary_CarriesTheFieldsARowNeeds()
    {
        var summary = MobileTemplateSummaryResponse.From(Orc());

        Assert.Equal("orc", summary.Id);
        Assert.Equal("an orc", summary.Name);
        Assert.Equal("the brute", summary.Title);
        Assert.Equal("monster", summary.Category);
        Assert.Equal(["monster", "green"], summary.Tags);
        Assert.Equal(17, summary.Body);
        Assert.Equal(96, summary.Strength);
    }

    [Fact]
    public void Summary_CountsTheVariants()
        => Assert.Equal(2, MobileTemplateSummaryResponse.From(Orc()).VariantCount);

    [Fact]
    public void Summary_AddressesTheTemplateArt()
        => Assert.Equal("/api/v1/images/mobiles/templates/orc.png", MobileTemplateSummaryResponse.From(Orc()).ImageUrl);

    // Ids come from YAML and are not guaranteed to be URL-safe.
    [Fact]
    public void Summary_EscapesAnIdThatNeedsIt()
    {
        var summary = MobileTemplateSummaryResponse.From(new() { Id = "orc chief" });

        Assert.Equal("/api/v1/images/mobiles/templates/orc%20chief.png", summary.ImageUrl);
    }

    [Fact]
    public void Detail_CarriesEveryFlatField()
    {
        var detail = MobileTemplateResponse.From(Orc());

        Assert.Equal("orc", detail.Id);
        Assert.Equal("orcs", detail.NamePool);
        Assert.Equal("A green brute.", detail.Description);
        Assert.Equal("base_monster", detail.BaseMobile);
        Assert.Equal(96, detail.Strength);
        Assert.Equal(70, detail.Dexterity);
        Assert.Equal(30, detail.Intelligence);
        Assert.Equal("orc_loot", detail.LootTableId);
        Assert.Equal("orc_brain", detail.BrainScript);
    }

    [Fact]
    public void Detail_NamesTheGender()
        => Assert.Equal("Male", MobileTemplateResponse.From(Orc()).Gender);

    // Gender is optional on a template — a spawn picks one. Null says "either", which is not the
    // same thing as male.
    [Fact]
    public void Detail_LeavesTheGenderNullWhenTheTemplateDoesNotFixOne()
        => Assert.Null(MobileTemplateResponse.From(new() { Id = "orc" }).Gender);

    [Fact]
    public void Detail_CarriesTheSkills()
    {
        var detail = MobileTemplateResponse.From(Orc());

        Assert.Equal(80, detail.Skills["Swordsmanship"]);
    }

    // Hues on a template are SPECS: a value or a range resolved per spawn. Reporting them as numbers
    // would mean picking one end of a range and lying about the other.
    [Fact]
    public void Detail_ReportsHueSpecsAsTheStringsTheyAre()
    {
        var detail = MobileTemplateResponse.From(Orc());

        Assert.Equal("0x455-0x45a", detail.Appearance.SkinHue);
        Assert.Equal("1102", detail.Appearance.HairHue);
        Assert.Equal(17, detail.Appearance.Body);
    }

    [Fact]
    public void Detail_CarriesTheEquipment()
    {
        var worn = Assert.Single(MobileTemplateResponse.From(Orc()).Equipment);

        Assert.Equal("orc_club", worn.Item);
        Assert.Equal("OneHanded", worn.Layer);
        Assert.Equal("0x0", worn.Hue);
    }

    [Fact]
    public void Detail_CarriesEachVariantWithItsOwnAppearanceAndEquipment()
    {
        var variants = MobileTemplateResponse.From(Orc()).Variants;

        Assert.Equal(["orc scout", "orc chief"], variants.Select(variant => variant.Name));

        var chief = variants[1];

        Assert.Equal(3, chief.Weight);
        Assert.Equal("Female", chief.Gender);
        Assert.Equal("chief_loot", chief.LootTableId);
        Assert.Equal(18, chief.Appearance.Body);
        Assert.Equal("orc_axe", Assert.Single(chief.Equipment).Item);
    }

    [Fact]
    public void Detail_AddressesBothPictures()
    {
        var detail = MobileTemplateResponse.From(Orc());

        Assert.Equal("/api/v1/images/mobiles/templates/orc.png", detail.ImageUrl);
        Assert.Equal("/api/v1/images/mobiles/templates/orc/paperdoll.png", detail.PaperdollUrl);
    }

    private static MobileTemplate Orc()
    {
        var template = new MobileTemplate
        {
            Id = "orc",
            Name = "an orc",
            NamePool = "orcs",
            Gender = MobileTemplateGenderType.Male,
            Title = "the brute",
            Category = "monster",
            Description = "A green brute.",
            Tags = ["monster", "green"],
            BaseMobile = "base_monster",
            Strength = 96,
            Dexterity = 70,
            Intelligence = 30,
            LootTableId = "orc_loot",
            BrainScript = "orc_brain",
            Appearance = new()
            {
                Body = 17,
                SkinHue = "0x455-0x45a",
                HairStyle = 8252,
                HairHue = "1102",
            },
            Equipment = [new() { Item = "orc_club", Layer = "OneHanded", Hue = "0x0" }],
        };

        template.Skills["Swordsmanship"] = 80;

        template.Variants.Add(new() { Name = "orc scout", Weight = 1 });
        template.Variants.Add(
            new()
            {
                Name = "orc chief",
                Weight = 3,
                Gender = MobileTemplateGenderType.Female,
                LootTableId = "chief_loot",
                Appearance = new() { Body = 18 },
                Equipment = [new() { Item = "orc_axe", Layer = "OneHanded" }],
            }
        );

        return template;
    }
}
