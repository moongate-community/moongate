using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class LootTemplatesLoaderTests
{
    public LootTemplatesLoaderTests()
    {
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
    }

    [Fact]
    public async Task LoadDataAsync_ReadsTablesFromSubfolders()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/loots/gems/gems.toml",
            "[[loot]]\nid = \"gems\"\n[[loot.entries]]\nweight = 80\n\n[[loot.entries]]\nweight = 20\nitem_id = \"ruby\"\namount = 2\n"
        );
        root.CreateFile(
            "templates/loots/rich.toml",
            "[[loot]]\nid = \"rich\"\n[[loot.entries]]\nloot_template_id = \"gems\"\n"
        );

        var tables = (await CreateLoader(root).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(
            (2, 80, "ruby"),
            (tables["gems"].Entries.Count, tables["gems"].Entries[0].Weight, tables["gems"].Entries[1].ItemId)
        );
        Assert.Equal("gems", Assert.Single(tables["rich"].Entries).LootTemplateId);
    }

    [Fact]
    public async Task LoadDataAsync_ATableWithNoEntries_IsKept()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/loots/a.toml", "[[loot]]\nid = \"journals\"\nentries = []\n");

        Assert.Empty(Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities).Entries);
    }

    [Theory,
     InlineData(
         "[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"ruby\"\n\n[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"ruby\"\n"
     ),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"ruby\"\nloot_template_id = \"a\"\n"),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"cape\"\n"),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nloot_template_id = \"missing\"\n"),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nweight = 0\nitem_id = \"ruby\"\n"),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"ruby\"\namount = 0\n"),
     InlineData("[[loot]]\nid = \"a\"\n[[loot.entries]]\nitem_id = \"ruby\"\namount = 70000\n"),
     InlineData(
         "[[loot]]\nid = \"b\"\n[[loot.entries]]\nitem_id = \"ruby\"\n\n[[loot]]\nid = \"a\"\n[[loot.entries]]\nloot_template_id = \"b\"\namount = 0\n"
     ),
     InlineData("[[loot]]\nid = \" \"\n[[loot.entries]]\nitem_id = \"ruby\"\n"),
     InlineData(
         "[[loot]]\nid = \"a\"\n[[loot.entries]]\nloot_template_id = \"b\"\n\n[[loot]]\nid = \"b\"\n[[loot.entries]]\nloot_template_id = \"a\"\n"
     )]
    public async Task LoadDataAsync_ABadTable_ThrowsInvalidDataException(string toml)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/loots/a.toml", toml);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_AnItemTemplateWhoseLootIsNoTable_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/loots/a.toml", "[[loot]]\nid = \"gems\"\n[[loot.entries]]\nitem_id = \"ruby\"\n");
        var chest = new ItemTemplate { Id = "chest", ItemId = new Serial(0x0E41), Loot = ["gems", "missing"] };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, chest).LoadDataAsync());

        Assert.Contains("chest", exception.Message);
        Assert.Contains("missing", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AnItemTemplateWhoseLootExists_Loads()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/loots/a.toml", "[[loot]]\nid = \"gems\"\n[[loot.entries]]\nitem_id = \"ruby\"\n");
        var chest = new ItemTemplate { Id = "chest", ItemId = new Serial(0x0E41), Loot = ["gems", "gems"] };

        Assert.Single((await CreateLoader(root, chest).LoadDataAsync()).Entities);
    }

    private static LootTemplatesLoader CreateLoader(TemporaryDirectory root, params ItemTemplate[] more)
    {
        return new(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService().With([new ItemTemplate { Id = "ruby", ItemId = new Serial(0x0F13) }, .. more])
        );
    }
}
