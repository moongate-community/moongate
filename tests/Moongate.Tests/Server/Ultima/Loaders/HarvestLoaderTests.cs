using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class HarvestLoaderTests
{
    private const string Fish = """
                                [[resource]]
                                id = "fish"
                                area = 8
                                amount_min = 5
                                amount_max = 15
                                respawn_min_minutes = 10
                                respawn_max_minutes = 20

                                """;

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheResources()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/harvest.toml", Fish + "[[resource]]\nid = \"ore\"\narea = 4\namount_min = 1\namount_max = 1\n");

        var resources = (await CreateLoader(root).LoadDataAsync()).Entities.ToArray();

        Assert.Equal(["fish", "ore"], resources.Select(resource => resource.Id));
        Assert.Equal(
            (8, 5, 15, 10, 20),
            (resources[0].Area, resources[0].AmountMin, resources[0].AmountMax, resources[0].RespawnMinMinutes,
                resources[0].RespawnMaxMinutes)
        );
        // A resource that names no time comes back at once.
        Assert.Equal((0, 0), (resources[1].RespawnMinMinutes, resources[1].RespawnMaxMinutes));
    }

    [Fact]
    public async Task LoadDataAsync_AResourceWithVeins_ReadsThemInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/harvest.toml",
            Fish +
            "[[resource]]\nid = \"wood\"\narea = 4\namount_min = 2\namount_max = 4\n" +
            "[[resource.vein]]\nid = \"plain\"\nweight = 490\n[[resource.vein]]\nid = \"oak\"\nweight = 300\n"
        );

        var resources = (await CreateLoader(root).LoadDataAsync()).Entities.ToArray();

        Assert.Empty(resources[0].Vein);
        Assert.Equal([("plain", 490), ("oak", 300)], resources[1].Vein.Select(vein => (vein.Id, vein.Weight)));
    }

    [Theory]
    [InlineData("id = \"Oak Tree\"\nweight = 1")]
    [InlineData("id = \"oak\"\nweight = 0")]
    [InlineData("id = \"oak\"\nweight = -3")]
    [InlineData("id = \"oak\"\nweight = 2000000")]
    [InlineData("id = \"plain\"\nweight = 5")]
    public async Task LoadDataAsync_ABadVein_StopsTheServer(string vein)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile(
            "data/harvest.toml",
            "[[resource]]\nid = \"wood\"\narea = 4\namount_min = 2\namount_max = 4\n" +
            "[[resource.vein]]\nid = \"plain\"\nweight = 490\n[[resource.vein]]\n" + vein + "\n"
        );

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_WithoutTheFile_HasNoResource()
    {
        using var root = new TemporaryDirectory();

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory]
    [InlineData("id = \"\"\narea = 8\namount_min = 1\namount_max = 2")]
    [InlineData("id = \"Fish Pond\"\narea = 8\namount_min = 1\namount_max = 2")]
    [InlineData("id = \"fish\"\narea = 0\namount_min = 1\namount_max = 2")]
    [InlineData("id = \"fish\"\narea = 300\namount_min = 1\namount_max = 2")]
    [InlineData("id = \"fish\"\narea = 8\namount_min = 0\namount_max = 2")]
    [InlineData("id = \"fish\"\narea = 8\namount_min = 5\namount_max = 4")]
    [InlineData("id = \"fish\"\narea = 8\namount_min = 1\namount_max = 2\nrespawn_min_minutes = -1")]
    [InlineData("id = \"fish\"\narea = 8\namount_min = 1\namount_max = 2\nrespawn_min_minutes = 9\nrespawn_max_minutes = 3")]
    [InlineData("id = \"fish\"\narea = 8\namount_min = 1\namount_max = 2\nrespawn_max_minutes = 2147483647")]
    public async Task LoadDataAsync_ABadResource_StopsTheServer(string resource)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/harvest.toml", "[[resource]]\n" + resource + "\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_TheSameIdTwice_StopsTheServer()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/harvest.toml", Fish + Fish);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static HarvestLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
