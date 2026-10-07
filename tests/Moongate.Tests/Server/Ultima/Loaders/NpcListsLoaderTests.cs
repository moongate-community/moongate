using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class NpcListsLoaderTests
{
    [Fact]
    public async Task LoadDataAsync_ReadsWeightedAndNestedLists_FromSubfolders()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "templates/npc_lists/lands/forest.toml",
            "[[npc_list]]\nid = \"forest\"\nentries = [{ mobile_id = \"orc\", weight = 3 }, { npc_list_id = \"trolls\" }]\n\n" +
            "[[npc_list]]\nid = \"trolls\"\nentries = [{ mobile_id = \"troll\" }]\n"
        );

        var lists = (await Loader(root).LoadDataAsync()).Entities.ToDictionary(list => list.Id);

        Assert.Equal(
            ["orc:3", "trolls:1"],
            lists["forest"].Entries.Select(entry => $"{entry.MobileId ?? entry.NpcListId}:{entry.Weight}")
        );
    }

    [Theory,
     InlineData("[[npc_list]]\nid = \"a\"\nentries = [{ mobile_id = \"dragon\" }]\n", "dragon"),
     InlineData("[[npc_list]]\nid = \"a\"\nentries = [{ npc_list_id = \"b\" }]\n", "'b'"),
     InlineData("[[npc_list]]\nid = \"a\"\nentries = []\n", "no entries"),
     InlineData("[[npc_list]]\nid = \"a\"\nentries = [{ mobile_id = \"orc\", npc_list_id = \"a\" }]\n", "both"),
     InlineData("[[npc_list]]\nid = \"a\"\nentries = [{ weight = 2 }]\n", "neither"),
     InlineData("[[npc_list]]\nid = \"a\"\nentries = [{ mobile_id = \"orc\", weight = 0 }]\n", "weight"),
     InlineData(
         "[[npc_list]]\nid = \"a\"\nentries = [{ npc_list_id = \"b\" }]\n\n[[npc_list]]\nid = \"b\"\nentries = [{ npc_list_id = \"a\" }]\n",
         "itself"
     ),
     InlineData(
         "[[npc_list]]\nid = \"a\"\nentries = [{ mobile_id = \"orc\" }]\n\n[[npc_list]]\nid = \"a\"\nentries = [{ mobile_id = \"orc\" }]\n",
         "twice"
     )]
    public async Task LoadDataAsync_ABrokenList_StopsTheLoad(string toml, string reason)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/npc_lists/lists.toml", toml);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());

        Assert.Contains(reason, error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_NoDirectory_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await Loader(root).LoadDataAsync()).Entities);
    }

    private static NpcListsLoader Loader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService().With(new MobileTemplate { Id = "orc" }, new MobileTemplate { Id = "troll" })
        );
    }
}
