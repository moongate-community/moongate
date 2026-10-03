using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class LocationsLoaderTests
{
    private const string Covetous = """
                                    [[location]]
                                    map = "felucca"
                                    category = "Dungeons/Covetous"
                                    name = "Entrance"
                                    location = "(2499, 919, 0)"

                                    [[location]]
                                    map = "termur"
                                    category = ""
                                    name = "Royal City"
                                    location = "(750, 3440, -20)"

                                    """;

    public LocationsLoaderTests()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
    }

    [Fact]
    public async Task LoadDataAsync_ValidFile_ReadsThePlacesInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/locations.toml", Covetous);
        var loader = CreateLoader(root);
        await loader.InitializeAsync();

        var places = (await loader.LoadDataAsync()).Entities;

        Assert.Equal(2, places.Count);
        Assert.Equal(
            (MapType.Felucca, "Dungeons/Covetous", "Entrance", new Point3D(2499, 919, 0)),
            (places[0].Map, places[0].Category, places[0].Name, places[0].Location)
        );
        Assert.Equal(
            (MapType.TerMur, "", "Royal City", new Point3D(750, 3440, -20)),
            (places[1].Map, places[1].Category, places[1].Name, places[1].Location)
        );
    }

    [Fact]
    public async Task LoadDataAsync_APlaceWithoutCategory_IsFiledUnderItsMap()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/locations.toml", Covetous.Replace("category = \"\"\n", ""));

        Assert.Equal("", (await CreateLoader(root).LoadDataAsync()).Entities[1].Category);
    }

    // A shard may delete the file: .go then travels by coordinates only.
    [Fact]
    public async Task LoadDataAsync_MissingFile_HasNoPlaces()
    {
        using var root = new TemporaryDirectory();
        var loader = CreateLoader(root);

        await loader.InitializeAsync();

        Assert.Empty((await loader.LoadDataAsync()).Entities);
    }

    [Theory,
     InlineData("map = \"felucca\"", ""),
     InlineData("name = \"Entrance\"", ""),
     InlineData("name = \"Entrance\"", "name = \"  \""),
     InlineData("location = \"(2499, 919, 0)\"", ""),
     InlineData("location = \"(2499, 919, 0)\"", "location = \"(2499, 919, 200)\""),
     InlineData("location = \"(2499, 919, 0)\"", "location = \"(-1, 919, 0)\""),
     InlineData("category = \"Dungeons/Covetous\"", "category = \"Dungeons//Covetous\""),
     InlineData("category = \"Dungeons/Covetous\"", "category = \"Dungeons/\"")]
    public async Task LoadDataAsync_ABadPlace_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/locations.toml", Covetous.Replace(from, to));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root).LoadDataAsync());

        Assert.Contains("locations.toml", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_AnUnknownMapName_Throws()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/locations.toml", Covetous.Replace("map = \"felucca\"", "map = \"atlantis\""));

        await Assert.ThrowsAnyAsync<Exception>(() => CreateLoader(root).LoadDataAsync());
    }

    private static LocationsLoader CreateLoader(TemporaryDirectory root)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]));
    }
}
