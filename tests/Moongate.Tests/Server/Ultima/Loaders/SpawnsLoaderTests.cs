using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class SpawnsLoaderTests
{
    private const string Shop =
        "[[spawn]]\nid = \"trammel_0\"\nname = \"The Hammer And Anvil\"\nmobile_ids = [\"orc\"]\nnpc_list_ids = [\"forest\"]\n" +
        "max = 2\nmin_minutes = 5\nmax_minutes = 10\ncall = 1\nareas = [{ x1 = 10, y1 = 20, x2 = 15, y2 = 25 }]\n" +
        "exclude = [{ x1 = 11, y1 = 21, x2 = 12, y2 = 22 }]\nonly_outside = true\n";

    [Fact]
    public async Task LoadDataAsync_ReadsTheSpawns_OnTheMapOfTheirFolder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/spawns/trammel/towns.toml", Shop);

        var spawn = Assert.Single((await Loader(root).LoadDataAsync()).Entities);

        Assert.Equal((MapType.Trammel, "The Hammer And Anvil", 2, true), (spawn.Map, spawn.Name, spawn.Max, spawn.OnlyOutside));
        Assert.Equal(["orc"], spawn.MobileIds);
        Assert.Equal(["forest"], spawn.NpcListIds);
        Assert.True(spawn.Areas[0].Contains(15, 25));
        Assert.True(spawn.Exclude[0].Contains(11, 21));
    }

    [Theory,
     InlineData("mobile_ids = [\"orc\"]", "mobile_ids = [\"dragon\"]", "dragon"),
     InlineData("npc_list_ids = [\"forest\"]", "npc_list_ids = [\"swamp\"]", "swamp"),
     InlineData("mobile_ids = [\"orc\"]\nnpc_list_ids = [\"forest\"]", "mobile_ids = []\nnpc_list_ids = []", "nothing to spawn"),
     InlineData("max = 2", "max = 0", "max"),
     InlineData("call = 1", "call = 0", "call"),
     InlineData("max_minutes = 10", "max_minutes = 4", "min_minutes"),
     InlineData("areas = [{ x1 = 10, y1 = 20, x2 = 15, y2 = 25 }]", "areas = []", "no area"),
     InlineData("areas = [{ x1 = 10, y1 = 20, x2 = 15, y2 = 25 }]", "areas = [{ x1 = 15, y1 = 20, x2 = 10, y2 = 25 }]", "area")]
    public async Task LoadDataAsync_ABrokenSpawn_StopsTheLoad(string from, string to, string reason)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/spawns/trammel/towns.toml", Shop.Replace(from, to));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());

        Assert.Contains(reason, error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AFolderThatIsNotAMap_OrATwiceUsedId_StopsTheLoad()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/spawns/sosaria/towns.toml", Shop);

        Assert.Contains("sosaria", (await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync())).Message);

        using var twice = new TemporaryDirectory();
        twice.CreateFile("templates/spawns/trammel/a.toml", Shop);
        twice.CreateFile("templates/spawns/trammel/b.toml", Shop);

        Assert.Contains("trammel_0", (await Assert.ThrowsAsync<InvalidDataException>(() => Loader(twice).LoadDataAsync())).Message);
    }

    [Fact]
    public async Task LoadDataAsync_NoDirectory_LoadsNothing()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await Loader(root).LoadDataAsync()).Entities);
    }

    private static SpawnsLoader Loader(TemporaryDirectory root)
    {
        return new(
            new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService()
                .With(new MobileTemplate { Id = "orc" })
                .With(new NpcListTemplate { Id = "forest", Entries = [new() { MobileId = "orc" }] })
        );
    }
}
