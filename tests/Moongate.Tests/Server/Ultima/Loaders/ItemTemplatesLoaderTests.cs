using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
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
