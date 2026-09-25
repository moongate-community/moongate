using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Characters;

public sealed class CharacterCreationRulesTests
{
    private static readonly BannedNamesContent BannedNames = new()
    {
        StartsWith = ["lord", "gm"],
        Words = ["mage", "grandmaster"]
    };

    private static readonly RaceContent Human = new()
    {
        Race = RaceType.Human,
        Name = "Human",
        SkinHues = [HueSpec.FromRange(0x03EA, 0x0422)],
        HairHues = [HueSpec.FromRange(0x044E, 0x047D)],
        Male = new() { Body = 400, Hair = [0x203B, 0x2048], Beard = [0x203E] },
        Female = new() { Body = 401, Hair = [0x203B, 0x2046], Beard = [] }
    };

    [Theory, InlineData(60, 20, 10), InlineData(30, 30, 30), InlineData(10, 20, 60)]
    public void ValidateStats_InRangeAndAddingUpTo90_AreKept(int str, int dex, int intel)
    {
        Assert.Equal((str, dex, intel), CharacterCreationRules.ValidateStats(str, dex, intel));
    }

    [Theory, InlineData(61, 19, 10), InlineData(9, 41, 40), InlineData(30, 30, 29), InlineData(45, 10, 10)]
    public void ValidateStats_OutOfRangeOrWrongTotal_FallBackToTen(int str, int dex, int intel)
    {
        Assert.Equal((10, 10, 10), CharacterCreationRules.ValidateStats(str, dex, intel));
    }

    [Theory, InlineData(50, 50, 0, 0), InlineData(50, 50, 20, 0), InlineData(30, 30, 30, 30)]
    public void ValidateSkills_TotalOf100Or120_KeepsTheSkillsWithAValue(int a, int b, int c, int d)
    {
        var skills = Skills((SkillType.Magery, a), (SkillType.Meditation, b), (SkillType.Wrestling, c), (SkillType.Tactics, d));

        var accepted = CharacterCreationRules.ValidateSkills(skills, RaceType.Human);

        Assert.Equal(skills.Where(skill => skill.Value > 0), accepted);
    }

    [Fact]
    public void ValidateSkills_ValueAbove50_IsNotValid()
    {
        var skills = Skills((SkillType.Magery, 51), (SkillType.Meditation, 49), (SkillType.Wrestling, 0), (SkillType.Tactics, 0));

        Assert.Empty(CharacterCreationRules.ValidateSkills(skills, RaceType.Human));
    }

    [Theory, InlineData(50, 40), InlineData(50, 60)]
    public void ValidateSkills_TotalNot100Or120_IsNotValid(int a, int b)
    {
        var skills = Skills((SkillType.Magery, a), (SkillType.Meditation, 30), (SkillType.Wrestling, b - 30), (SkillType.Tactics, 0));

        Assert.Empty(CharacterCreationRules.ValidateSkills(skills, RaceType.Human));
    }

    [Fact]
    public void ValidateSkills_SameSkillTwice_IsNotValid()
    {
        var skills = Skills((SkillType.Magery, 50), (SkillType.Magery, 50), (SkillType.Wrestling, 0), (SkillType.Tactics, 0));

        Assert.Empty(CharacterCreationRules.ValidateSkills(skills, RaceType.Human));
    }

    [Theory, InlineData(SkillType.Stealth), InlineData(SkillType.RemoveTrap), InlineData(SkillType.Spellweaving)]
    public void ValidateSkills_SkillNotAllowedAtCreation_IsNotValid(SkillType skill)
    {
        var skills = Skills((skill, 50), (SkillType.Magery, 50), (SkillType.Wrestling, 0), (SkillType.Tactics, 0));

        Assert.Empty(CharacterCreationRules.ValidateSkills(skills, RaceType.Human));
    }

    [Fact]
    public void ValidateSkills_UnknownSkillId_IsNotValid()
    {
        var skills = Skills(((SkillType)200, 50), (SkillType.Magery, 50), (SkillType.Wrestling, 0), (SkillType.Tactics, 0));

        Assert.Empty(CharacterCreationRules.ValidateSkills(skills, RaceType.Human));
    }

    [Theory,
     InlineData(SkillType.Throwing, RaceType.Human, false),
     InlineData(SkillType.Throwing, RaceType.Gargoyle, true),
     InlineData(SkillType.Archery, RaceType.Gargoyle, false),
     InlineData(SkillType.Archery, RaceType.Elf, true)]
    public void ValidateSkills_RaceSpecificSkill_IsDroppedWithoutInvalidatingTheOthers(
        SkillType skill,
        RaceType race,
        bool kept
    )
    {
        var skills = Skills((skill, 50), (SkillType.Magery, 50), (SkillType.Wrestling, 0), (SkillType.Tactics, 0));

        var accepted = CharacterCreationRules.ValidateSkills(skills, race);

        Assert.Contains(accepted, choice => choice.Skill == SkillType.Magery);
        Assert.Equal(kept, accepted.Any(choice => choice.Skill == skill));
    }

    [Theory,
     InlineData("Aria"),
     InlineData("Mary Jane"),
     InlineData("O'Brien"),
     InlineData("Jean-Luc"),
     InlineData("St.Clair"),
     InlineData("Ab"),
     InlineData("Abcdefghijklmnop"),
     InlineData("Magenta")]
    public void ValidateName_AllowedNames_AreKept(string name)
    {
        Assert.Equal(name, CharacterCreationRules.ValidateName(name, BannedNames));
    }

    [Theory,
     InlineData(null),
     InlineData(""),
     InlineData("A"),
     InlineData("Abcdefghijklmnopq"),
     InlineData("St. Clair"),
     InlineData("-Aria"),
     InlineData("Ar--ia"),
     InlineData("Ar ia 2"),
     InlineData("Aria_"),
     InlineData("Lord Aria"),
     InlineData("GMaria"),
     InlineData("Aria the Mage"),
     InlineData("Grandmaster"),
     InlineData("Lordaria")]
    public void ValidateName_NotAllowed_BecomesTheDefaultName(string? name)
    {
        Assert.Equal(CharacterCreationRules.DefaultName, CharacterCreationRules.ValidateName(name, BannedNames));
    }

    [Fact]
    public void ValidateName_SurroundingSpaces_AreTrimmed()
    {
        Assert.Equal("Aria", CharacterCreationRules.ValidateName("  Aria  ", BannedNames));
    }

    [Theory,
     InlineData(0x03F0, 0x83F0),
     InlineData(0x0100, 0x83EA),
     InlineData(0x0500, 0x8422),
     InlineData(0x83F0, 0x83F0)]
    public void ValidateSkinHue_ClipsToTheRaceAndSetsThePartialFlag(int sent, int expected)
    {
        Assert.Equal(new Hue((ushort)expected), CharacterCreationRules.ValidateSkinHue(Human, new((ushort)sent)));
    }

    [Theory, InlineData(0, 2), InlineData(1, 2), InlineData(500, 500), InlineData(1001, 1001), InlineData(2000, 1001)]
    public void ValidateClothingHue_ClipsToTheDyeableRange(int sent, int expected)
    {
        Assert.Equal(new Hue((ushort)expected), CharacterCreationRules.ValidateClothingHue(new((ushort)sent)));
    }

    [Fact]
    public void ValidateHair_StyleOfTheGender_IsKeptWithAClippedHue()
    {
        Assert.Equal(
            (0x2046, new Hue(0x047D)),
            CharacterCreationRules.ValidateHair(Human, GenderType.Female, 0x2046, new(0x0999))
        );
    }

    [Theory, InlineData(GenderType.Male, 0x2046), InlineData(GenderType.Female, 0x2048), InlineData(GenderType.Male, 0)]
    public void ValidateHair_StyleNotAllowedOrNone_GivesNoHair(GenderType gender, int style)
    {
        Assert.Equal((0, Hue.None), CharacterCreationRules.ValidateHair(Human, gender, style, new(0x0450)));
    }

    [Fact]
    public void ValidateBeard_FemaleHuman_GetsNoBeard()
    {
        Assert.Equal((0, Hue.None), CharacterCreationRules.ValidateBeard(Human, GenderType.Female, 0x203E, new(0x0450)));
        Assert.Equal(
            (0x203E, new Hue(0x0450)),
            CharacterCreationRules.ValidateBeard(Human, GenderType.Male, 0x203E, new(0x0450))
        );
    }

    private static CharacterSkillChoice[] Skills(params (SkillType Skill, int Value)[] skills)
    {
        return skills.Select(skill => new CharacterSkillChoice { Skill = skill.Skill, Value = (byte)skill.Value }).ToArray();
    }
}
