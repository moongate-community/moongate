using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class MoongatesLoaderTests
{
    private const string Malas = """
                                 [[facet]]
                                 map = "malas"
                                 cliloc = 1060643
                                 selected_cliloc = 1062039

                                 [[facet.destination]]
                                 name = "Luna"
                                 cliloc = 1060641
                                 location = "(1015, 527, -65)"

                                 [[facet.destination]]
                                 name = "Umbra"
                                 cliloc = 1060642
                                 location = "(1997, 1386, -85)"
                                 hue = 0x0497
                                 average_z = true

                                 """;

    public MoongatesLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsTheFacetAndItsDestinationsInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", Malas);

        var malas = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal((MapType.Malas, 1060643, 1062039), (malas.Map, malas.Cliloc, malas.SelectedCliloc));
        Assert.Equal(["Luna", "Umbra"], malas.Destination.Select(destination => destination.Name));
        var umbra = malas.Destination[1];
        Assert.Equal(
            (1060642, new Point3D(1997, 1386, -85), 0x497, true),
            (umbra.Cliloc, umbra.Location, umbra.Hue, umbra.AverageZ)
        );
        Assert.Equal((0, false), (malas.Destination[0].Hue, malas.Destination[0].AverageZ));
    }

    [Fact]
    public async Task LoadDataAsync_AnEmptyFile_HasNoMoongates()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", "# no moongates on this shard\n");

        Assert.Empty((await CreateLoader(root).LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("map = \"malas\"", ""),
     InlineData("location = \"(1015, 527, -65)\"", ""),
     InlineData("cliloc = 1060643", "cliloc = 0"),
     InlineData("selected_cliloc = 1062039", "selected_cliloc = 0"),
     InlineData("name = \"Luna\"", "name = \"\""),
     InlineData("cliloc = 1060641", "cliloc = 0"),
     InlineData("location = \"(1015, 527, -65)\"", "location = \"(1015, 527, 200)\""),
     InlineData("location = \"(1015, 527, -65)\"", "location = \"(-1, 527, -65)\""),
     InlineData("hue = 0x0497", "hue = 70000")]
    public async Task LoadDataAsync_ABadEntry_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", Malas.Replace(from, to));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("moongates.toml", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AnUnknownMapName_Throws()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", Malas.Replace("map = \"malas\"", "map = \"atlantis\""));

        await Assert.ThrowsAnyAsync<Exception>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_TheSameMapTwice_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", Malas + Malas);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_AFacetWithoutDestinations_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/moongates.toml", "[[facet]]\nmap = \"malas\"\ncliloc = 1060643\nselected_cliloc = 1062039\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    private static MoongatesLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
