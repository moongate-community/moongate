using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class StartingItemsLoaderTests
{
    public StartingItemsLoaderTests()
    {
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEverySet()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/starting_items.toml",
            "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\n\n" +
            "[[set]]\nskill = \"alchemy\"\n[[set.items]]\nitems = [\"robe\"]\namount = \"1d3\"\nequip = true\n"
        );

        var sets = (await CreateLoader(root).LoadDataAsync()).Entities;

        Assert.Equal(2, sets.Count);
        Assert.True(sets[0].Common);
        Assert.Equal((SkillType?)SkillType.Alchemy, sets[1].Skill);
    }

    [Theory,
     InlineData("[[set]]\ncommon = true\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"cape\"]\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = \"1d3-3\"\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = 65536\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = [\"dagger\"]\namount = \"65000+1d1000\"\n"),
     InlineData("[[set]]\n[[set.items]]\nitems = [\"dagger\"]\n"),
     InlineData("[[set]]\ncommon = true\n[[set.items]]\nitems = []\n")]
    public async Task LoadDataAsync_ABadSet_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_items.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Theory]
    [InlineData("book_template = \"missing\"\nbook_values = { contact_name = \"Vega\" }")]
    [InlineData("book_template = \"welcome_letter\"")]
    [InlineData("book_template = \"welcome_letter\"\nbook_values = { contact_name = \"Vega\", extra = 1 }")]
    [InlineData("book_template = \"welcome_letter\"\nbook_values = { contact_name = [1, 2] }")]
    [InlineData("book_template = \"welcome_letter\"\nbook_values = { contact_name = nan }")]
    [InlineData("book_template = \"welcome_letter\"\nbook_values = { contact_name = \"a\\u0000b\" }")]
    [InlineData("book_values = { contact_name = \"Vega\" }")]
    [InlineData("book_template = \"\"")]
    public async Task LoadDataAsync_InvalidBookBinding_IdentifiesTheStartingItemsFile(string fields)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_items.toml", "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"scroll\"]\n" + fields);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
        Assert.Contains("starting_items.toml", error.Message);
    }

    [Theory]
    [InlineData("dagger", false)]
    [InlineData("stacked_scroll", false)]
    [InlineData("scroll", true)]
    public async Task LoadDataAsync_BookOnUnsuitableItem_IsRefused(string item, bool equip)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_items.toml", "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"" + item + "\"]\n" +
            "book_template = \"welcome_letter\"\nbook_values = { contact_name = \"Vega\" }\nequip = " + equip.ToString().ToLowerInvariant());
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Theory]
    [InlineData("7", "Meet 7")]
    [InlineData("true", "Meet true")]
    [InlineData("\"Vega $player_name\"", "Meet Vega $player_name")]
    public async Task LoadDataAsync_CustomScalarValues_RenderAsLiteralText(string value, string expected)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_items.toml", "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"scroll\"]\n" +
            "book_template = \"welcome_letter\"\nbook_values = { contact_name = " + value + " }");
        var set = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);
        var entry = Assert.Single(set.Items);
        var data = new StubDataLoaderService().With(new BookTemplate { Id = "welcome_letter", Title = "Hello $player_name", Content = "Meet $contact_name", Variables = ["contact_name"] });
        Assert.True(new BookTemplateService(data).TryRender(entry.BookTemplate!, new TextTemplateContext { PlayerName = "Aria" }, "eng", entry.BookValues, out var rendered));
        Assert.Equal(expected, rendered!.Content);
    }

    private static StartingItemsLoader CreateLoader(TemporaryDirectory root)
    {
        var data = new StubDataLoaderService().With(
                new ItemTemplate { Id = "dagger", ItemId = new Serial(0x0F52) },
                new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03) },
                new ItemTemplate { Id = "scroll", ItemId = new(0x14ED), Stackable = false, ScriptId = "readable_scroll" },
                new ItemTemplate { Id = "stacked_scroll", ItemId = new(0x14ED), Stackable = true, ScriptId = "readable_scroll" }
            ).With(new BookTemplate { Id = "welcome_letter", ItemTemplate = "scroll", Title = "Welcome $player_name", Content = "Meet $contact_name", Variables = ["contact_name"] });
        return new(new DirectoriesConfig(root.Path, ["data"]), data, new BookTemplateService(data));
    }
}
