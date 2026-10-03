using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Locations;
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
        var root = Service(Place(MapType.Trammel, "Towns", "Britain"), Place(MapType.Felucca, "Towns", "Britain")).GetNode("");

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
        // The test sectors hold Trammel and Felucca: Malas is not loaded.
        var service = Service(Place(MapType.Malas, "Towns", "Luna"), Place(MapType.Felucca, "Towns", "Cove"));

        Assert.Equal(["Felucca"], service.GetNode("")!.Categories);
        Assert.Null(service.GetNode("malas"));
    }

    [Fact]
    public void GetNode_AMapWhoseFilesAreNotOpen_IsLeftOut()
    {
        // The fake map service opens Felucca only.
        var service = new LocationService(
            new StubDataLoaderService().With(Place(MapType.Trammel, "Towns", "Cove"), Place(MapType.Felucca, "Towns", "Cove")),
            TestSectors.Create(),
            new FakeMapService(16, 16)
        );

        Assert.Equal(["Felucca"], service.GetNode("")!.Categories);
    }

    [Fact]
    public void GetNode_APlaceOutsideItsMap_IsLeftOut()
    {
        var outside = new NamedLocation { Map = MapType.Felucca, Category = "Towns", Name = "Nowhere", Location = new Point3D(9000, 10, 0) };

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
        var service = Service(Place(MapType.Felucca, "Towns/Britain", "Bank"), Place(MapType.Felucca, "Factions/Towns", "Britain"));

        Assert.Equal("Factions/Towns", Assert.Single(service.Find("britain", MapType.Felucca)).Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("atlantis")]
    public void Find_NothingToFind_IsEmpty(string text)
    {
        Assert.Empty(Service(Place(MapType.Felucca, "Towns", "Cove")).Find(text, MapType.Felucca));
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
