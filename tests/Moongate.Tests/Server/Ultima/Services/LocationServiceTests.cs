using Moongate.Core.Directories;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class LocationServiceTests
{
    [Fact]
    public void GetNode_TheRoot_ListsTheMapsThatHavePlaces_InFileOrder()
    {
        var root = Service(Place(MapType.Trammel, "Towns", "Britain"), Place(MapType.Felucca, "Towns", "Britain"))
            .GetNode("");

        Assert.NotNull(root);
        Assert.Equal(("", ""), (root.Path, root.Name));
        Assert.Equal(["Trammel", "Felucca"], root.Categories);
        Assert.Empty(root.Locations);
    }

    [Fact]
    public void GetNode_AMap_ListsItsCategoriesAndItsOwnPlaces_InFileOrder()
    {
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain", "Bank"),
            Place(MapType.Felucca, "", "Arena"),
            Place(MapType.Felucca, "Dungeons/Covetous", "Entrance"),
            Place(MapType.Felucca, "Towns", "Cove")
        );

        var felucca = service.GetNode("felucca");

        Assert.NotNull(felucca);
        Assert.Equal(("Felucca", "Felucca"), (felucca.Path, felucca.Name));
        Assert.Equal(["Towns", "Dungeons"], felucca.Categories);
        Assert.Equal(["Arena"], felucca.Locations.Select(place => place.Name));
    }

    [Fact]
    public void GetNode_ACategory_IsFoundWhateverTheCase_AndHoldsItsCategoriesAndPlaces()
    {
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain", "Bank"),
            Place(MapType.Felucca, "Towns", "Cove"),
            Place(MapType.Felucca, "Towns/Britain", "Cemetery")
        );

        var towns = service.GetNode("FELUCCA/towns");
        var britain = service.GetNode("felucca/Towns/britain");

        Assert.NotNull(towns);
        Assert.Equal(("Felucca/Towns", "Towns"), (towns.Path, towns.Name));
        Assert.Equal(["Britain"], towns.Categories);
        Assert.Equal(["Cove"], towns.Locations.Select(place => place.Name));
        Assert.NotNull(britain);
        Assert.Equal("Felucca/Towns/Britain", britain.Path);
        Assert.Empty(britain.Categories);
        Assert.Equal(["Bank", "Cemetery"], britain.Locations.Select(place => place.Name));
    }

    [Theory]
    [InlineData("malas")]
    [InlineData("felucca/Nowhere")]
    [InlineData("atlantis")]
    public void GetNode_AnUnknownPath_IsNull(string path)
    {
        Assert.Null(Service(Place(MapType.Felucca, "Towns", "Cove")).GetNode(path));
    }

    [Fact]
    public void GetNode_ThePlacesOfAMapThatIsNotLoaded_AreLeftOut()
    {
        // The test sectors hold Trammel, Felucca, Ilshenar and Malas: Tokuno is not loaded.
        var service = Service(Place(MapType.Tokuno, "Towns", "Luna"), Place(MapType.Felucca, "Towns", "Cove"));

        Assert.Equal(["Felucca"], service.GetNode("")!.Categories);
        Assert.Null(service.GetNode("tokuno"));
    }

    [Fact]
    public void GetNode_AMapWhoseFilesAreNotOpen_IsLeftOut()
    {
        // The fake map service opens Felucca only.
        var service = new LocationService(
            new StubDataLoaderService().With(
                Place(MapType.Trammel, "Towns", "Cove"),
                Place(MapType.Felucca, "Towns", "Cove")
            ),
            TestSectors.Create(),
            new FakeMapService(16, 16)
        );

        Assert.Equal(["Felucca"], service.GetNode("")!.Categories);
    }

    [Fact]
    public void GetNode_APlaceOutsideItsMap_IsLeftOut()
    {
        var outside = new NamedLocation
            { Map = MapType.Felucca, Category = "Towns", Name = "Nowhere", Location = new Point3D(9000, 10, 0) };

        var service = Service(outside, Place(MapType.Felucca, "Towns", "Cove"));

        Assert.Equal(["Cove"], service.GetNode("felucca/towns")!.Locations.Select(place => place.Name));
    }

    [Fact]
    public void Find_AName_IsFoundWhateverTheCase()
    {
        var service = Service(Place(MapType.Felucca, "Towns", "Cove"), Place(MapType.Felucca, "Towns", "Buccaneer's Den"));

        Assert.Equal("Buccaneer's Den", Assert.Single(service.Find("  buccaneer's   DEN ", MapType.Felucca)).Name);
    }

    [Fact]
    public void Find_ANameSeveralPlacesHave_GivesThemAll_AndTheWordsOfTheCategoryNarrowIt()
    {
        var service = Service(
            Place(MapType.Felucca, "Dungeons/Covetous", "Entrance"),
            Place(MapType.Felucca, "Dungeons/Shame", "Entrance"),
            Place(MapType.Felucca, "Dungeons/Shame", "Level 1")
        );

        Assert.Equal(2, service.Find("entrance", MapType.Felucca).Count);
        Assert.Equal("Dungeons/Shame", Assert.Single(service.Find("shame entrance", MapType.Felucca)).Category);
        Assert.Equal("Level 1", Assert.Single(service.Find("dungeons shame level 1", MapType.Felucca)).Name);
    }

    // As the title of the gump writes a level.
    [Theory]
    [InlineData("dungeons/covetous/entrance")]
    [InlineData("Covetous / Entrance")]
    [InlineData("/covetous/entrance/")]
    public void Find_APathWrittenWithSlashes_IsReadAsItsWords(string text)
    {
        var service = Service(
            Place(MapType.Felucca, "Dungeons/Covetous", "Entrance"),
            Place(MapType.Felucca, "Dungeons/Shame", "Entrance")
        );

        Assert.Equal("Dungeons/Covetous", Assert.Single(service.Find(text, MapType.Felucca)).Category);
    }

    [Fact]
    public void Find_OnlyWholeWordsCount()
    {
        var service = Service(Place(MapType.Felucca, "Towns", "Cove"), Place(MapType.Felucca, "Towns", "Alcove"));

        Assert.Equal("Cove", Assert.Single(service.Find("cove", MapType.Felucca)).Name);
        Assert.Empty(service.Find("ove", MapType.Felucca));
    }

    [Fact]
    public void Find_ThePlacesOfTheOwnMap_ComeBeforeThoseOfTheOthers()
    {
        var service = Service(Place(MapType.Felucca, "Towns", "Cove", 10), Place(MapType.Trammel, "Towns", "Cove", 20));

        Assert.Equal(MapType.Trammel, Assert.Single(service.Find("cove", MapType.Trammel)).Map);
        Assert.Equal(MapType.Felucca, Assert.Single(service.Find("cove", MapType.Felucca)).Map);
    }

    [Fact]
    public void Find_APlaceOfAnotherMapOnly_IsFound()
    {
        var service = Service(Place(MapType.Felucca, "Towns", "Cove"), Place(MapType.Trammel, "Towns", "Haven"));

        Assert.Equal(MapType.Trammel, Assert.Single(service.Find("haven", MapType.Felucca)).Map);
    }

    [Fact]
    public void Find_ACategoryWithAPlaceCalledCenter_GivesThatOne_NotItsFirst()
    {
        // ModernUO lists Britain's places by name: the first is a castle's upper floor.
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain", "Blackthorn Castle"),
            Place(MapType.Felucca, "Towns/Britain", "Center"),
            Place(MapType.Felucca, "Towns/Britain", "Park")
        );

        Assert.Equal("Center", Assert.Single(service.Find("britain", MapType.Felucca)).Name);
        // Named itself, the castle is still reached.
        Assert.Equal("Blackthorn Castle", Assert.Single(service.Find("blackthorn castle", MapType.Felucca)).Name);
    }

    [Fact]
    public void Find_ACategoryNoPlaceIsNamedAs_GivesItsFirstPlace()
    {
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain/Shops", "Baker"),
            Place(MapType.Felucca, "Towns/Britain", "Bank"),
            Place(MapType.Felucca, "Towns/Cove", "Gate")
        );

        Assert.Equal("Bank", Assert.Single(service.Find("britain", MapType.Felucca)).Name);
        Assert.Equal("Baker", Assert.Single(service.Find("britain shops", MapType.Felucca)).Name);
    }

    [Fact]
    public void Find_APlaceNamedAsTheText_WinsOverACategory()
    {
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain", "Bank"),
            Place(MapType.Felucca, "Factions/Towns", "Britain")
        );

        Assert.Equal("Factions/Towns", Assert.Single(service.Find("britain", MapType.Felucca)).Category);
    }

    // As the shipped data: Felucca has the faction town Britain, Trammel only the category.
    [Fact]
    public void Find_ACategoryOfTheOwnMap_WinsOverAPlaceOfAnotherMap()
    {
        var service = Service(
            Place(MapType.Felucca, "Factions/Towns", "Britain"),
            Place(MapType.Trammel, "Towns/Britain", "Bank")
        );

        var found = Assert.Single(service.Find("britain", MapType.Trammel));

        Assert.Equal((MapType.Trammel, "Bank"), (found.Map, found.Name));
    }

    [Fact]
    public void Find_ACategory_WinsOverAPlaceWhoseNameOnlyEndsWithTheText()
    {
        var service = Service(
            Place(MapType.Felucca, "Dungeons/Tomb of Kings", "Gate to Stygian Abyss"),
            Place(MapType.Felucca, "Dungeons/Stygian Abyss", "Entrance"),
            Place(MapType.Felucca, "Towns", "Old Haven"),
            Place(MapType.Felucca, "Towns/Haven", "Bank")
        );

        Assert.Equal("Entrance", Assert.Single(service.Find("stygian abyss", MapType.Felucca)).Name);
        Assert.Equal("Bank", Assert.Single(service.Find("haven", MapType.Felucca)).Name);
    }

    [Fact]
    public void Find_TheLastWordsOfAName_FindThePlace_WhenNothingIsNamedSo()
    {
        var service = Service(Place(MapType.Felucca, "Towns", "Old Haven"), Place(MapType.Felucca, "Towns", "Cove"));

        Assert.Equal("Old Haven", Assert.Single(service.Find("haven", MapType.Felucca)).Name);
    }

    [Fact]
    public void Find_SeveralCategories_GiveTheirFirstPlacesInFileOrder()
    {
        var service = Service(
            Place(MapType.Felucca, "Towns/Britain", "Bank"),
            Place(MapType.Felucca, "Factions/Britain", "Base"),
            Place(MapType.Felucca, "Towns/Britain", "Inn")
        );

        Assert.Equal(["Bank", "Base"], service.Find("britain", MapType.Felucca).Select(place => place.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("atlantis")]
    public void Find_NothingToFind_IsEmpty(string text)
    {
        Assert.Empty(Service(Place(MapType.Felucca, "Towns", "Cove")).Find(text, MapType.Felucca));
    }

    // The places the documentation of go names, on the shipped file.
    [Theory]
    [InlineData("britain", MapType.Trammel, MapType.Trammel, "Towns/Britain")]
    [InlineData("britain", MapType.Felucca, MapType.Felucca, "Factions/Towns")]
    [InlineData("haven", MapType.Trammel, MapType.Trammel, "Towns/Haven")]
    [InlineData("covetous entrance", MapType.Felucca, MapType.Felucca, "Dungeons/Covetous")]
    [InlineData("covetous", MapType.Trammel, MapType.Trammel, "Dungeons/Covetous")]
    public async Task Find_OnTheShippedFile_GoesWhereTheWordsSay(string text, MapType own, MapType map, string category)
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(directory!.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        var loader = new LocationsLoader(new DirectoriesConfig(Path.Combine(directory.FullName, "moongate_root"), ["data"]));
        var places = (await loader.LoadDataAsync()).Entities.ToArray();

        var found = Assert.Single(Service(places).Find(text, own));

        Assert.Equal((map, category), (found.Map, found.Category));
    }

    private static LocationService Service(params NamedLocation[] places)
    {
        return new(new StubDataLoaderService().With(places), TestSectors.Create());
    }

    private static NamedLocation Place(MapType map, string category, string name, int x = 100)
    {
        return new() { Map = map, Category = category, Name = name, Location = new Point3D(x, 100, 0) };
    }
}
