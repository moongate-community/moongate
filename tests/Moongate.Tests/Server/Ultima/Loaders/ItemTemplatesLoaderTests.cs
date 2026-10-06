using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class ItemTemplatesLoaderTests
{
    public ItemTemplatesLoaderTests()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public async Task LoadDataAsync_LootAndGold_AreReadAndInherited()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/a.toml",
            "[[item]]\nid = \"chest\"\nitem_id = 0x0E41\nloot = [\"gems\", \"gems\"]\ngold = \"1d100+29\"\n\n" +
            "[[item]]\nid = \"old_chest\"\nbase_id = \"chest\"\nitem_id = 0\n\n" +
            "[[item]]\nid = \"poor_chest\"\nbase_id = \"chest\"\nitem_id = 0\nloot = []\ngold = \"0\"\n\n" +
            "[[item]]\nid = \"box\"\nitem_id = 0x09A8\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(["gems", "gems"], templates["chest"].Loot);
        Assert.Equal((30, 129), (templates["chest"].Gold!.Value.Min, templates["chest"].Gold!.Value.Max));
        Assert.Equal(["gems", "gems"], templates["old_chest"].Loot);
        Assert.Equal(129, templates["old_chest"].Gold!.Value.Max);
        // A copy: one template's list is not its parent's.
        Assert.NotSame(templates["chest"].Loot, templates["old_chest"].Loot);
        Assert.Empty(templates["poor_chest"].Loot!);
        Assert.Equal(0, templates["poor_chest"].Gold!.Value.Max);
        Assert.Null(templates["box"].Loot);
        Assert.Null(templates["box"].Gold);
    }

    [Fact]
    public async Task LoadDataAsync_GoldThatCanRollBelowZero_Throws()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/a.toml", "[[item]]\nid = \"chest\"\nitem_id = 0x0E41\ngold = \"1d4-2\"\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("gold", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_Dyeable_IsInheritedFromTheBase_UnlessTheTemplateSaysOtherwise()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/clothes.toml",
            "[[item]]\nid = \"base_clothing\"\nitem_id = 0x1517\ndyeable = true\n\n" +
            "[[item]]\nid = \"shirt\"\nbase_id = \"base_clothing\"\nitem_id = 0x1517\n\n" +
            "[[item]]\nid = \"death_robe\"\nbase_id = \"base_clothing\"\nitem_id = 0x204E\ndyeable = false\n\n" +
            "[[item]]\nid = \"sword\"\nitem_id = 0x0F5E\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(
            ((bool?)true, (bool?)false, (bool?)null),
            (templates["shirt"].Dyeable, templates["death_robe"].Dyeable, templates["sword"].Dyeable)
        );
    }

    [Fact]
    public async Task LoadDataAsync_TheCombatFields_AreReadAndInherited_AndATemplateKeepsItsOwn()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/swords.toml",
            "[[item]]\nid = \"base_longsword\"\nitem_id = 0x0F60\nlayer = \"one_handed\"\nweapon_type = \"sword\"\n\n" +
            "[[item]]\nid = \"longsword\"\nbase_id = \"base_longsword\"\nitem_id = 0x0F60\ndamage_min = 5\ndamage_max = 33\n" +
            "speed = 35\nstrength_required = 25\nmax_hits = 90\n\n" +
            "[[item]]\nid = \"fast_longsword\"\nbase_id = \"longsword\"\nitem_id = 0x0F60\nspeed = 50\n\n" +
            "[[item]]\nid = \"ringmail_tunic\"\nitem_id = 0x13EC\nlayer = \"inner_torso\"\narmor_rating = 22\nstrength_required = 20\nmax_hits = 51\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var longsword = templates["longsword"];
        Assert.Equal(
            (WeaponType.Sword, 5, 33, 35, 25, 90),
            (longsword.WeaponType!.Value, longsword.DamageMin!.Value, longsword.DamageMax!.Value, longsword.Speed!.Value,
                longsword.StrengthRequired!.Value, longsword.MaxHits!.Value)
        );
        var fast = templates["fast_longsword"];
        Assert.Equal((WeaponType.Sword, 5, 33, 50), (fast.WeaponType!.Value, fast.DamageMin!.Value, fast.DamageMax!.Value, fast.Speed!.Value));
        var tunic = templates["ringmail_tunic"];
        Assert.Equal((22, 20, 51, (WeaponType?)null, (int?)null), (tunic.ArmorRating!.Value, tunic.StrengthRequired!.Value, tunic.MaxHits!.Value, tunic.WeaponType, tunic.DamageMax));
        Assert.Null(templates["base_longsword"].DamageMax);
    }

    [Theory]
    [InlineData("damage_min = -1")]
    [InlineData("damage_min = 10\ndamage_max = 5")]
    [InlineData("damage_max = 70000")]
    [InlineData("speed = 0")]
    [InlineData("speed = 1000")]
    [InlineData("armor_rating = -1")]
    [InlineData("armor_rating = 1000")]
    [InlineData("strength_required = -1")]
    [InlineData("max_hits = -1")]
    public async Task LoadDataAsync_ACombatFieldOutOfRange_IsRefused(string field)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/bad.toml", $"[[item]]\nid = \"bad\"\nitem_id = 0x0F60\n{field}\n");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("bad", error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_ResolvesBaseIdThroughThreeLevels_AndReadsSubfolders()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/base.toml", "[[item]]\nid = \"base_item\"\nitem_id = 0\nstackable = false\ndecays = false\n");
        root.CreateFile(
            "templates/items/clothes/shirts.toml",
            "[[item]]\nid = \"base_shirt\"\nbase_id = \"base_item\"\nitem_id = 0x1517\nlayer = \"shirt\"\nweight = 1.0\n\n" +
            "[[item]]\nid = \"fancy_shirt\"\nbase_id = \"base_shirt\"\nname = \"fancy shirt\"\nitem_id = 0x1EFD\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var fancy = templates["fancy_shirt"];
        Assert.Equal(
            (0x1EFDu, "fancy shirt", (LayerType?)LayerType.Shirt, (decimal?)1.0m, (bool?)false, (bool?)false),
            (fancy.ItemId.Value, fancy.Name, fancy.Layer, fancy.Weight, fancy.Stackable, fancy.Decays)
        );
    }

    [Fact]
    public async Task LoadDataAsync_TwoHandedWeapon_IsReadAndInherited()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/a.toml",
            "[[item]]\nid = \"base_bow\"\nitem_id = 0x13B2\nlayer = \"two_handed\"\ntwo_handed_weapon = true\n\n" +
            "[[item]]\nid = \"elven_bow\"\nbase_id = \"base_bow\"\nitem_id = 0\n\n" +
            "[[item]]\nid = \"heater\"\nitem_id = 0x1B76\nlayer = \"two_handed\"\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.True(templates["elven_bow"].TwoHandedWeapon);
        Assert.Null(templates["heater"].TwoHandedWeapon);
    }

    [Fact]
    public async Task LoadDataAsync_ZeroItemId_TakesTheParentGraphic()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/a.toml", "[[item]]\nid = \"coin\"\nitem_id = 0x0EED\n\n[[item]]\nid = \"big_coin\"\nbase_id = \"coin\"\nitem_id = 0\n");

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(0x0EEDu, templates["big_coin"].ItemId.Value);
    }

    [Fact]
    public async Task LoadDataAsync_AnUnsetHue_TakesTheParentHue()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/a.toml",
            "[[item]]\nid = \"box\"\nitem_id = 1\nhue = 0x089E\n\n[[item]]\nid = \"puzzle_box\"\nbase_id = \"box\"\nitem_id = 1\n\n" +
            "[[item]]\nid = \"plain_box\"\nbase_id = \"box\"\nitem_id = 1\nhue = 0\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(0x089E, templates["puzzle_box"].Hue!.Value.Resolve().Value);
        Assert.Equal(0, templates["plain_box"].Hue!.Value.Resolve().Value);
    }

    [Fact]
    public async Task LoadDataAsync_ChildTags_ReplaceTheParentTags()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/a.toml",
            "[[item]]\nid = \"p\"\nitem_id = 1\ntags = { a = \"1\" }\n\n[[item]]\nid = \"c\"\nbase_id = \"p\"\nitem_id = 1\ntags = { b = \"2\" }\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(["b"], templates["c"].Tags!.Keys);
    }

    [Fact]
    public async Task LoadDataAsync_InheritedTags_AreACopy()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/items/a.toml",
            "[[item]]\nid = \"p\"\nitem_id = 1\ntags = { a = \"1\" }\n\n[[item]]\nid = \"c\"\nbase_id = \"p\"\nitem_id = 1\n"
        );

        var templates = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);
        templates["c"].Tags!["b"] = "2";

        Assert.Equal(["a"], templates["p"].Tags!.Keys);
    }

    [Theory,
     InlineData("[[item]]\nid = \"a\"\nitem_id = 1\n", "[[item]]\nid = \"a\"\nitem_id = 2\n"),
     InlineData("[[item]]\nid = \"a\"\nbase_id = \"missing\"\nitem_id = 1\n", ""),
     InlineData("[[item]]\nid = \"a\"\nbase_id = \"b\"\nitem_id = 1\n", "[[item]]\nid = \"b\"\nbase_id = \"a\"\nitem_id = 1\n"),
     InlineData("[[item]]\nid = \" \"\nitem_id = 1\n", "")]
    public async Task LoadDataAsync_ABadTemplateSet_ThrowsInvalidDataException(string first, string second)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/one.toml", first);
        root.CreateFile("templates/items/two.toml", second);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_NoDirectory_ReturnsNoTemplates()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    private static ItemTemplatesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["templates"]));
    }
}
