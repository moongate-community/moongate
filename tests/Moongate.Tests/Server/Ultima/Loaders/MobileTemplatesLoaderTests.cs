using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class MobileTemplatesLoaderTests
{
    public MobileTemplatesLoaderTests()
    {
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
    }

    [Fact]
    public async Task LoadDataAsync_InheritsFieldsSkillsResistancesSoundsAndTags_AndReplacesEquipment()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/mobiles/orcs.toml",
            """
            [[mobile]]
            id = "base_orc"
            name_list = "orc"
            body = 17
            script_id = "wander"
            flee_at = -1
            control_slots = 3
            blood_hue = 68
            strength = "1d25+95"
            skills = { tactics = "60", wrestling = "50" }
            resistances = { fire = "20", cold = "10" }
            sounds = { idle = 0x45A, death = 0x45D }
            tags = { a = "1" }
            equipment = [{ items = ["club"] }]

            [[mobile]]
            id = "orc_captain"
            base_id = "base_orc"
            title = "the Captain"
            skills = { tactics = "90" }
            resistances = { fire = "30" }
            sounds = { idle = 0x45B }
            tags = { b = "2" }
            equipment = [{ items = ["axe"] }]
            """
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var captain = templates["orc_captain"];
        Assert.Equal(
            ("orc", (int?)17, "1d25+95", "the Captain"),
            (captain.NameList, captain.Body, captain.Strength.ToString(), captain.Title)
        );
        Assert.Equal(("90", "50"), (captain.Skills!["tactics"].ToString(), captain.Skills["wrestling"].ToString()));
        Assert.Equal(("30", "10"), (captain.Resistances!.Fire.ToString(), captain.Resistances.Cold.ToString()));
        Assert.Equal(((int?)0x45B, (int?)0x45D), (captain.Sounds!.Idle, captain.Sounds.Death));
        Assert.Equal(["a", "b"], captain.Tags!.Keys.Order());
        Assert.Equal(["axe"], Assert.Single(captain.Equipment!).Items);
        Assert.Equal("wander", captain.ScriptId);
        Assert.Equal(-1, captain.FleeAt);
        Assert.Equal(3, captain.ControlSlots);
        Assert.Equal(68, captain.BloodHue);

        captain.Skills["wrestling"] = DiceSpec.FromValue(1);
        Assert.Equal("50", templates["base_orc"].Skills!["wrestling"].ToString());
    }

    [Fact]
    public async Task LoadDataAsync_ReadsAndInheritsWhetherTheMobilesOpenDoors()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/mobiles/doors.toml",
            """
            [[mobile]]
            id = "base_brute"
            body = 1
            opens_doors = false

            [[mobile]]
            id = "ogre"
            base_id = "base_brute"

            [[mobile]]
            id = "clever_cat"
            body = 201
            opens_doors = true

            [[mobile]]
            id = "orc"
            body = 17
            """
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(
            [false, true, null],
            new[] { "ogre", "clever_cat", "orc" }.Select(id => templates[id].OpensDoors)
        );
    }

    [Fact]
    public async Task LoadDataAsync_InheritsTheMovement()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/mobiles/sea.toml",
            """
            [[mobile]]
            id = "base_serpent"
            body = 150
            movement = "water"

            [[mobile]]
            id = "sea_serpent"
            base_id = "base_serpent"

            [[mobile]]
            id = "walrus"
            body = 221
            movement = "both"

            [[mobile]]
            id = "orc"
            body = 17
            """
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(
            [MobileMovementType.Water, MobileMovementType.Both, null],
            new[] { "sea_serpent", "walrus", "orc" }.Select(id => templates[id].Movement)
        );
    }

    [Theory,
     InlineData("[[mobile]]\nid = \"a\"\nbody = 1\n\n[[mobile]]\nid = \"a\"\nbody = 2\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbase_id = \"missing\"\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbase_id = \"b\"\n\n[[mobile]]\nid = \"b\"\nbase_id = \"a\"\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbody = 1\nname_list = \"elf\"\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbody = 1\nequipment = [{ items = [\"cape\"] }]\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbody = 1\nskills = { flying = \"10\" }\n"),
     InlineData("[[mobile]]\nid = \"a\"\nbody = 1\nloot = [\"treasure\"]\n")]
    public async Task LoadDataAsync_ABadTemplate_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/mobiles/a.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_AChildWithoutItsOwnSkills_GetsACopy()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/mobiles/a.toml",
            "[[mobile]]\nid = \"p\"\nbody = 1\nskills = { tactics = \"60\" }\n\n[[mobile]]\nid = \"c\"\nbase_id = \"p\"\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);
        templates["c"].Skills!["tactics"] = DiceSpec.FromValue(1);

        Assert.Equal("60", templates["p"].Skills!["tactics"].ToString());
    }

    [Fact]
    public async Task LoadDataAsync_TheGenderNameListWithoutAFemaleList_Throws()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/mobiles/a.toml", "[[mobile]]\nid = \"guard\"\nbody = 400\nname_list = \"{gender}\"\n");
        var loader = new MobileTemplatesLoader(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService().With(new NameList { Id = "male", Names = ["Aaron"] })
        );

        Assert.Contains("female", (await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadDataAsync())).Message);
    }

    [Fact]
    public async Task LoadDataAsync_AKnownLootTable_IsAccepted()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/mobiles/a.toml", "[[mobile]]\nid = \"orc\"\nbody = 17\nloot = [\"gems\", \"gems\"]\n");

        Assert.Equal(["gems", "gems"], Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities).Loot);
    }

    [Fact]
    public async Task LoadDataAsync_TheGenderNameList_IsAccepted()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/mobiles/a.toml", "[[mobile]]\nid = \"guard\"\nbody = 400\nname_list = \"{gender}\"\n");

        Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    private static MobileTemplatesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService()
                .With(
                    new NameList { Id = "orc", Names = ["Grok"] },
                    new NameList { Id = "male", Names = ["Aaron"] },
                    new NameList { Id = "female", Names = ["Alice"] }
                )
                .With(
                    new ItemTemplate { Id = "club", ItemId = new Serial(0x13B4) },
                    new ItemTemplate { Id = "axe", ItemId = new Serial(0x0F49) }
                )
                .With(new LootTemplate { Id = "gems", Entries = [new LootEntry()] })
        );
    }
}
