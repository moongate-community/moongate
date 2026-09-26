using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class ContainersLoaderTests
{
    private const string Default = """
                                   [[container]]
                                   gump = 0x3C
                                   bounds = "(44, 65)..(186, 159)"
                                   drop_sound = 0x48
                                   default = true
                                   items = []

                                   """;

    private const string Backpack = """
                                    [[container]]
                                    name = "backpack"
                                    gump = 0x3C
                                    bounds = "(44, 65)..(186, 159)"
                                    drop_sound = 0x48
                                    items = [0x0E75, 0x09B2]

                                    """;

    private const string GameBoard = """
                                     [[container]]
                                     gump = 0x91A
                                     bounds = "(0, 0)..(282, 210)"
                                     items = [0x0FA6]

                                     """;

    public ContainersLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Rectangle2DTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEveryEntry()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/containers.toml", Default + Backpack + GameBoard);

        var result = await CreateLoader(root).LoadDataAsync();

        Assert.Equal(3, result.Entities.Count);
        var fallback = Assert.Single(result.Entities, container => container.Default);
        Assert.Equal(0x3C, fallback.Gump);
        Assert.Equal(new Rectangle2D(44, 65, 142, 94), fallback.Bounds);
        Assert.Equal(0x48, fallback.DropSound);
        Assert.Null(fallback.Name);
        Assert.Equal("backpack", result.Entities[1].Name);
        Assert.Equal([0x0E75, 0x09B2], result.Entities[1].Items);
        Assert.Null(result.Entities[2].DropSound);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task LoadDataAsync_NotExactlyOneDefault_ThrowsInvalidDataException(bool twoDefaults)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/containers.toml", twoDefaults ? Default + Default : Backpack);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ItemListedTwice_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/containers.toml", Default + Backpack + GameBoard.Replace("0x0FA6", "0x09B2"));

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("0x9B2", exception.Message, StringComparison.Ordinal);
    }

    [Theory, InlineData("gump = 0x91A", "gump = 0"), InlineData("\"(0, 0)..(282, 210)\"", "\"(0, 0)..(0, 210)\"")]
    public async Task LoadDataAsync_NoGumpOrEmptyArea_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/containers.toml", Default + GameBoard.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static ContainersLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
