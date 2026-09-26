using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class StartingCitiesLoaderTests
{
    private const string Britain = """
                                   [[starting_city]]
                                   town = "Britain"
                                   description = "The Wayfarer's Inn"
                                   location = "(1602, 1591, 20)"
                                   map = "trammel"
                                   cliloc = 1075074

                                   """;

    public StartingCitiesLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
    }

    [Fact]
    public async Task InitializeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root).InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsEveryField()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_cities.toml", Britain);

        var britain = Assert.Single((await CreateLoader(root).LoadDataAsync()).Entities);

        Assert.Equal("Britain", britain.Town);
        Assert.Equal("The Wayfarer's Inn", britain.Description);
        Assert.Equal((1602, 1591, 20), (britain.Location.X, britain.Location.Y, britain.Location.Z));
        Assert.Equal(MapType.Trammel, britain.Map);
        Assert.Equal(1075074u, britain.Cliloc.Value);
    }

    [Fact]
    public async Task LoadDataAsync_NoCities_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_cities.toml", "# no cities\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_MoreCitiesThanTheListHolds_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_cities.toml", string.Concat(Enumerable.Repeat(Britain, 256)));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());
    }

    [Theory,
     InlineData("town = \"Britain\"", "town = \"\""),
     InlineData("town = \"Britain\"", "town = \"Città\""),
     InlineData("description = \"The Wayfarer's Inn\"", "description = \"The Wayfarer's Inn by the old stone bridge\"")]
    public async Task LoadDataAsync_InvalidCityText_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/starting_cities.toml", Britain.Replace(from, to));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("ASCII", exception.Message);
    }

    private static StartingCitiesLoader CreateLoader(TemporaryDirectory root)
    {
        return new StartingCitiesLoader(new DirectoriesConfig(root.Path, ["data"]));
    }
}
