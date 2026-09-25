using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;
using Tomlyn;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class RegionsLoaderTests
{
    private const string Town = """
                                [[region]]
                                name = "Britain"
                                type = "town"
                                priority = 50
                                areas = [{ x1 = 1416, y1 = 1498, x2 = 1740, y2 = 1777, z1 = -10, z2 = 128 }]
                                go_location = "(1495, 1629, 10)"
                                music = "Britain1"
                                guarded = true
                                housing = false

                                """;

    private const string Tavern = """
                                  [[region]]
                                  name = "The Tavern"
                                  type = "base"
                                  parent = "Britain"
                                  areas = [{ x1 = 1450, y1 = 1600, x2 = 1460, y2 = 1610 }]
                                  instant_logout = true

                                  """;

    public RegionsLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_OneFilePerMap_ReadsRegionsWithTheirMap()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town + Tavern);
        root.CreateFile("data/regions/felucca.toml", Town);

        var result = await CreateLoader(root).LoadDataAsync();

        Assert.Equal(3, result.Entities.Count);
        var britain = result.Entities.First(region => region.Map == MapType.Trammel && region.Name == "Britain");
        Assert.Equal(RegionType.Town, britain.Type);
        Assert.Equal(new Point3D(1495, 1629, 10), britain.GoLocation);
        Assert.Equal(MusicType.Britain1, britain.Music);
        Assert.True(britain.Guarded);
        Assert.False(britain.Housing);
        Assert.True(britain.RecallIn);
        Assert.True(britain.TeleportIn);
        Assert.True(britain.TeleportOut);
        Assert.Single(result.Entities, region => region.Map == MapType.Felucca);
        var tavern = result.Entities.First(region => region.Name == "The Tavern");
        Assert.Equal("Britain", tavern.Parent);
        Assert.Null(tavern.GoLocation);
        Assert.Null(tavern.Music);
        Assert.Equal(50, tavern.Priority);
        Assert.True(tavern.InstantLogout);
    }

    [Theory,
     InlineData(1500, 1600, 0, true),
     InlineData(1740, 1600, 0, false),
     InlineData(1500, 1600, -20, false),
     InlineData(1500, 1600, 128, false)]
    public async Task Contains_UsesIncludedStartExcludedEndAndHeight(int x, int y, int z, bool expected)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town);

        var britain = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal(expected, britain.Contains(x, y, z));
    }

    [Fact]
    public async Task LoadDataAsync_FileNotNamedAfterAMap_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/britannia.toml", Town);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Theory,
     InlineData("x2 = 1740", "x2 = 1416"),
     InlineData("z1 = -10, z2 = 128", "z1 = 128, z2 = -10"),
     InlineData("areas = [{ x1 = 1416, y1 = 1498, x2 = 1740, y2 = 1777, z1 = -10, z2 = 128 }]", "areas = []")]
    public async Task LoadDataAsync_BadArea_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_NameUsedTwiceOnAMap_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town + Town);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ParentOnAnotherMapOnly_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Tavern);
        root.CreateFile("data/regions/felucca.toml", Town);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ParentLoop_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town.Replace("type = \"town\"", "type = \"town\"\nparent = \"The Tavern\"") + Tavern);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_UnknownMusic_ThrowsTomlException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/regions/trammel.toml", Town.Replace("\"Britain1\"", "\"Jazz\""));

        await Assert.ThrowsAsync<TomlException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static RegionsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
