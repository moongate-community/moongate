using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class JailLoaderTests
{
    private const string Jail = """
                                map = "felucca"
                                release = "(1444, 1697, 10)"

                                [[cell]]
                                number = 1
                                location = "(5276, 1164, 0)"

                                [[cell]]
                                number = 2
                                location = "(5286, 1164, 0)"

                                """;

    public JailLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheMapTheReleaseSpotAndTheCellsInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/jail.toml", Jail);

        var jail = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal((MapType.Felucca, new Point3D(1444, 1697, 10)), (jail.Map, jail.Release));
        Assert.Equal([1, 2], jail.Cell.Select(cell => cell.Number));
        Assert.Equal(new Point3D(5286, 1164, 0), jail.Cell[1].Location);
    }

    [Fact]
    public async Task LoadDataAsync_NoFile_GivesNoJail()
    {
        using var root = new TemporaryDirectory();
        var loader = CreateLoader(root);

        await loader.InitializeAsync();

        Assert.Empty((await loader.LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("map = \"felucca\"", ""),
     InlineData("release = \"(1444, 1697, 10)\"", ""),
     InlineData("release = \"(1444, 1697, 10)\"", "release = \"(1444, 1697, 200)\""),
     InlineData("number = 2", "number = 1"),
     InlineData("number = 2", "number = 0"),
     InlineData("location = \"(5286, 1164, 0)\"", ""),
     InlineData("location = \"(5286, 1164, 0)\"", "location = \"(-1, 1164, 0)\""),
     InlineData("location = \"(5286, 1164, 0)\"", "location = \"(70000, 1164, 0)\"")]
    public async Task LoadDataAsync_ABadEntry_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/jail.toml", Jail.Replace(from, to));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("jail.toml", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_NoCell_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/jail.toml", "map = \"felucca\"\nrelease = \"(1444, 1697, 10)\"\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static JailLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
