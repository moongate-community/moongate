using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class CraftsLoaderTests
{
    private const string Resources =
        "[[resource]]\nid = \"wood\"\ntemplates = [\"0x1bd7_board\"]\n[[resource]]\nid = \"cloth\"\ntemplates = [\"0x175d_cloth\"]\n";

    private const string Head = "id = \"carpentry\"\nname = \"Carpentry\"\nskill = \"carpentry\"\nsound = 0x023D\n[[group]]\nname = \"Chairs\"\n";

    private const string Stool =
        "[[group.recipe]]\nname = \"Stool\"\nitem = \"0x0a2a\"\nskill_min = 11.0\nskill_max = 36.0\nresources = [{ resource = \"wood\", amount = 9 }]\nskills = []\n";

    private const string Lute =
        "[[group.recipe]]\nname = \"Lute\"\nitem = \"0x0eb3_lute\"\nskill_min = 68.4\nskill_max = 93.4\n" +
        "resources = [{ resource = \"wood\", amount = 25 }, { resource = \"cloth\", amount = 10 }]\n" +
        "skills = [{ skill = \"musicianship\", min = 45.0, max = 70.0 }]\n";

    [Fact]
    public async Task LoadDataAsync_ValidFiles_ReadTheCraftAndItsLists()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources);
        root.CreateFile("data/crafts/carpentry.toml", Head + Stool + Lute);

        var (lists, crafts) = await Load(root);

        Assert.Equal(["wood", "cloth"], lists.Select(list => list.Id));
        Assert.Equal(["0x1bd7_board"], lists[0].Templates);
        var craft = Assert.Single(crafts);
        Assert.Equal(("carpentry", "Carpentry", "carpentry", 0x023D), (craft.Id, craft.Name, craft.Skill, craft.Sound));
        var group = Assert.Single(craft.Group);
        Assert.Equal("Chairs", group.Name);
        Assert.Equal(["Stool", "Lute"], group.Recipe.Select(recipe => recipe.Name));
        var lute = group.Recipe[1];
        Assert.Equal(("0x0eb3_lute", 68.4, 93.4), (lute.Item, lute.SkillMin, lute.SkillMax));
        Assert.Equal([("wood", 25), ("cloth", 10)], lute.Resources.Select(resource => (resource.Resource, resource.Amount)));
        Assert.Equal(("musicianship", 45.0, 70.0), (lute.Skills[0].Skill, lute.Skills[0].Min, lute.Skills[0].Max));
    }

    [Fact]
    public async Task LoadDataAsync_WithoutTheFolder_HasNoCraft()
    {
        using var root = new TemporaryDirectory();

        var (lists, crafts) = await Load(root);

        Assert.Empty(lists);
        Assert.Empty(crafts);
    }

    [Theory]
    [InlineData("skill = \"carpentry\"", "skill = \"woodworking\"")]
    [InlineData("item = \"0x0a2a\"", "item = \"0x7777\"")]
    [InlineData("skill_min = 11.0", "skill_min = 40.0")]
    [InlineData("skill_max = 36.0", "skill_max = 151.0")]
    [InlineData("resource = \"wood\", amount = 9", "resource = \"gems\", amount = 9")]
    [InlineData("resource = \"wood\", amount = 9", "resource = \"wood\", amount = 0")]
    [InlineData("resources = [{ resource = \"wood\", amount = 9 }]", "resources = []")]
    [InlineData("skills = []", "skills = [{ skill = \"juggling\", min = 1.0, max = 2.0 }]")]
    [InlineData("skills = []", "skills = [{ skill = \"tailoring\", min = 50.0, max = 20.0 }]")]
    [InlineData("name = \"Stool\"", "name = \"\"")]
    [InlineData("name = \"Chairs\"", "name = \"\"")]
    [InlineData("id = \"carpentry\"", "id = \"Car Pentry\"")]
    [InlineData("name = \"Carpentry\"", "name = \"\"")]
    public async Task LoadDataAsync_ABadCraft_StopsTheServer_NamingTheFile(string good, string bad)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources);
        root.CreateFile("data/crafts/carpentry.toml", (Head + Stool).Replace(good, bad));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Load(root));

        Assert.Contains("carpentry.toml", exception.Message);
    }

    [Theory]
    [InlineData("templates = [\"0x1bd7_board\"]", "templates = [\"0x9999\"]")]
    [InlineData("templates = [\"0x1bd7_board\"]", "templates = []")]
    [InlineData("id = \"cloth\"", "id = \"wood\"")]
    [InlineData("id = \"cloth\"", "id = \"Fine Cloth\"")]
    public async Task LoadDataAsync_ABadResourceList_StopsTheServer_NamingTheFile(string good, string bad)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources.Replace(good, bad));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => Load(root));

        Assert.Contains("resources.toml", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_SkillNames_AreKeptAsScriptsReadThem()
    {
        // A skill may be written in any case: scripts read the skills of a mobile by their snake_case names.
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources);
        root.CreateFile(
            "data/crafts/carpentry.toml",
            (Head + Lute).Replace("skill = \"carpentry\"", "skill = \"Carpentry\"").Replace("\"musicianship\"", "\"Musicianship\"")
        );

        var craft = Assert.Single((await Load(root)).Crafts);

        Assert.Equal(("carpentry", "musicianship"), (craft.Skill, craft.Group[0].Recipe[0].Skills[0].Skill));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[[group]]\nname = \"Chairs\"\n")]
    public async Task LoadDataAsync_ACraftWithoutGroupsOrAGroupWithoutRecipes_StopsTheServer(string groups)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources);
        root.CreateFile("data/crafts/carpentry.toml", "id = \"carpentry\"\nname = \"Carpentry\"\nskill = \"carpentry\"\nsound = 0x023D\n" + groups);

        await Assert.ThrowsAsync<InvalidDataException>(() => Load(root));
    }

    [Fact]
    public async Task LoadDataAsync_TheSameCraftInTwoFiles_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/crafts/resources.toml", Resources);
        root.CreateFile("data/crafts/carpentry.toml", Head + Stool);
        root.CreateFile("data/crafts/woodwork.toml", Head + Stool);

        await Assert.ThrowsAsync<InvalidDataException>(() => Load(root));
    }

    // The resource lists first, as the server loads them, then the crafts that name them.
    private static async Task<(List<CraftResourceList> Lists, List<CraftDefinition> Crafts)> Load(TemporaryDirectory root)
    {
        var directories = new DirectoriesConfig(root.Path, ["data"]);
        var data = new StubDataLoaderService().With(
            new ItemTemplate { Id = "0x0a2a", ItemId = new Serial(0x0A2A) },
            new ItemTemplate { Id = "0x0eb3_lute", ItemId = new Serial(0x0EB3) },
            new ItemTemplate { Id = "0x1bd7_board", ItemId = new Serial(0x1BD7) },
            new ItemTemplate { Id = "0x175d_cloth", ItemId = new Serial(0x175D) }
        );
        var lists = (await new CraftResourcesLoader(directories, data).LoadDataAsync()).Entities.ToList();
        data.With(lists.ToArray());
        var crafts = (await new CraftsLoader(directories, data).LoadDataAsync()).Entities.ToList();

        return (lists, crafts);
    }
}
