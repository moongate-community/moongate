using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Data.Templates.Mobiles;

public sealed class MobileTemplateTests
{
    public MobileTemplateTests()
    {
        AppContext.SetSwitch("Tomlyn.TomlSerializer.IsReflectionEnabledByDefault", true);
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public void EveryField_Toml_RoundTrip()
    {
        const string toml = """
                            id = "guard"
                            base_id = "base_human"
                            comment = "town guard"
                            name_list = "{gender}"
                            title = "the guard"
                            body = 400
                            gender = "random"
                            race = "human"
                            skin_hue = "0x03EA-0x0422"
                            hair = [0x203B, 0x203C]
                            hair_hue = 0x044E
                            beard = [0x203E]
                            beard_hue = 0x044E
                            strength = "1d25+95"
                            dexterity = 80
                            intelligence = "2d6"
                            hits = 150
                            mana = 0
                            stamina = "1d10+90"
                            damage = "3d4"
                            armor = 20
                            notoriety = "invulnerable"
                            karma = -2500
                            fame = "1d100"
                            loot = ["guard_loot"]
                            gold = "1d50"
                            script_id = "ai.guard"
                            visibility = "game_master"

                            [skills]
                            tactics = "1d21+79"
                            swordsmanship = 100

                            [resistances]
                            physical = "1d6+24"
                            fire = 10

                            [sounds]
                            start_attack = 0x1B0
                            death = 0x1B4

                            [tags]
                            post = "britain"

                            [[equipment]]
                            items = ["leather_skirt", "leather_shorts"]
                            gender = "female"

                            [[equipment]]
                            items = ["platemail_chest"]
                            hue = "0x0400-0x0420"
                            """;

        var template = TomlUtils.Deserialize<MobileTemplate>(toml)!;
        var back = TomlUtils.Deserialize<MobileTemplate>(TomlUtils.Serialize(template))!;

        foreach (var loaded in new[] { template, back })
        {
            Assert.Equal(("guard", "base_human", "{gender}", "the guard"), (loaded.Id, loaded.BaseId, loaded.NameList, loaded.Title));
            Assert.Equal((400, MobileGenderType.Random, RaceType.Human), (loaded.Body, loaded.Gender, loaded.Race));
            Assert.Equal([0x203B, 0x203C], loaded.Hair);
            Assert.Equal((96, 120), (loaded.Strength!.Value.Min, loaded.Strength.Value.Max));
            Assert.Equal(80, loaded.Dexterity!.Value.Roll());
            Assert.Equal(-2500, loaded.Karma!.Value.Roll());
            Assert.Equal(NotorietyType.Invulnerable, loaded.Notoriety);
            Assert.Equal((80, 100), (loaded.Skills!["tactics"].Min, loaded.Skills["tactics"].Max));
            Assert.Equal(100, loaded.Skills["swordsmanship"].Roll());
            Assert.Equal((25, 30), (loaded.Resistances!.Physical!.Value.Min, loaded.Resistances.Physical.Value.Max));
            Assert.Null(loaded.Resistances.Cold);
            Assert.Equal((0x1B0, null, 0x1B4), (loaded.Sounds!.StartAttack, loaded.Sounds.Idle, loaded.Sounds.Death));
            Assert.Equal("britain", loaded.Tags!["post"]);
            Assert.Equal(["guard_loot"], loaded.Loot);
            Assert.Equal(AccountType.GameMaster, loaded.Visibility);
            Assert.Equal(2, loaded.Equipment!.Count);
            Assert.Equal((GenderType.Female, (HueSpec?)null), (loaded.Equipment[0].Gender, loaded.Equipment[0].Hue));
            Assert.Null(loaded.Equipment[1].Gender);
        }
    }

    [Fact]
    public void UnsetFields_AreNotWritten()
    {
        var toml = TomlUtils.Serialize(new MobileTemplate { Id = "orc", Equipment = [new() { Items = ["club"] }] });

        foreach (var key in new[] { "base_id", "name", "body", "gender", "race", "strength", "hits", "skills", "resistances", "sounds", "notoriety", "karma", "loot", "gold", "visibility", "tags", "hue" })
        {
            Assert.DoesNotContain($"{key} =", toml);
            Assert.DoesNotContain($"[{key}]", toml);
        }
    }

    [Theory,
     InlineData("strength = \"1d6-10\"\n", "strength"),
     InlineData("hits = -1\n", "hits"),
     InlineData("gold = -5\n", "gold"),
     InlineData("[skills]\nnot_a_skill = 50\n", "skills"),
     InlineData("[skills]\ntactics = \"1d30+100\"\n", "skills"),
     InlineData("[resistances]\nfire = 101\n", "resistances"),
     InlineData("[sounds]\ndeath = -1\n", "sounds"),
     InlineData("[tags]\n\" \" = \"x\"\n", "tags"),
     InlineData("[[equipment]]\nitems = []\n", "equipment"),
     InlineData("[[equipment]]\nitems = [\"\"]\n", "equipment")]
    public void Validate_ABadValue_NamesTheTemplateAndField(string fields, string field)
    {
        var template = TomlUtils.Deserialize<MobileTemplate>("id = \"orc\"\n" + fields)!;

        var error = Assert.Throws<InvalidDataException>(template.Validate);

        Assert.StartsWith($"Mobile template 'orc': {field} ", error.Message);
    }

    [Fact]
    public void Validate_NegativeKarma_Passes()
    {
        TomlUtils.Deserialize<MobileTemplate>("id = \"orc\"\nkarma = -2500\n")!.Validate();
    }

    [Fact]
    public void Validate_AValidTemplate_Passes()
    {
        TomlUtils.Deserialize<MobileTemplate>("id = \"orc\"\nstrength = \"1d25+95\"\n[skills]\ntactics = 80\n[[equipment]]\nitems = [\"club\"]\n")!.Validate();
    }
}
