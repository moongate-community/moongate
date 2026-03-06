using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.World;
using Moongate.Server.FileLoaders;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.FileLoaders;

public class LocationsDataLoaderTests
{
    private sealed class TestLocationCatalogService : ILocationCatalogService
    {
        public List<WorldLocationEntry> Locations { get; } = [];

        public IReadOnlyList<WorldLocationEntry> GetAllLocations()
            => Locations;

        public void SetLocations(IReadOnlyList<WorldLocationEntry> locations)
        {
            Locations.Clear();
            Locations.AddRange(locations);
        }
    }

    [Test]
    public async Task LoadAsync_ShouldFlattenNestedCategoriesIntoCategoryPath()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var locationsPath = Path.Combine(directories[DirectoryType.Data], "locations");
        Directory.CreateDirectory(locationsPath);

        const string trammelJson = """
                                   {
                                     "name": "Trammel",
                                     "categories": [
                                       {
                                         "name": "Dungeons",
                                         "categories": [
                                           {
                                             "name": "Despise",
                                             "locations": [
                                               { "name": "Entrance", "location": [1298, 1080, 0] }
                                             ]
                                           }
                                         ]
                                       }
                                     ]
                                   }
                                   """;

        await File.WriteAllTextAsync(Path.Combine(locationsPath, "trammel.json"), trammelJson);

        var locationCatalogService = new TestLocationCatalogService();
        var loader = new LocationsDataLoader(directories, locationCatalogService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(locationCatalogService.Locations, Has.Count.EqualTo(1));
                Assert.That(locationCatalogService.Locations[0].MapId, Is.EqualTo(1));
                Assert.That(locationCatalogService.Locations[0].CategoryPath, Is.EqualTo("Dungeons / Despise"));
                Assert.That(locationCatalogService.Locations[0].Name, Is.EqualTo("Entrance"));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldImportLocationsWithMapIdDerivedFromFileName()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var locationsPath = Path.Combine(directories[DirectoryType.Data], "locations");
        Directory.CreateDirectory(locationsPath);

        const string feluccaJson = """
                                   {
                                     "name": "Felucca",
                                     "categories": [
                                       {
                                         "name": "Towns",
                                         "locations": [
                                           { "name": "Britain Bank", "location": [1496, 1628, 20] }
                                         ]
                                       }
                                     ]
                                   }
                                   """;

        await File.WriteAllTextAsync(Path.Combine(locationsPath, "felucca.json"), feluccaJson);

        var locationCatalogService = new TestLocationCatalogService();
        var loader = new LocationsDataLoader(directories, locationCatalogService);

        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(locationCatalogService.Locations, Has.Count.EqualTo(1));
                Assert.That(locationCatalogService.Locations[0].MapId, Is.EqualTo(0));
                Assert.That(locationCatalogService.Locations[0].MapName, Is.EqualTo("Felucca"));
                Assert.That(locationCatalogService.Locations[0].CategoryPath, Is.EqualTo("Towns"));
                Assert.That(locationCatalogService.Locations[0].Name, Is.EqualTo("Britain Bank"));
                Assert.That(locationCatalogService.Locations[0].Location.X, Is.EqualTo(1496));
                Assert.That(locationCatalogService.Locations[0].Location.Y, Is.EqualTo(1628));
                Assert.That(locationCatalogService.Locations[0].Location.Z, Is.EqualTo(20));
            }
        );
    }

    [Test]
    public async Task LoadAsync_ShouldSkipUnknownMapFileNames()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var locationsPath = Path.Combine(directories[DirectoryType.Data], "locations");
        Directory.CreateDirectory(locationsPath);

        const string json = """
                            {
                              "name": "Unknown",
                              "categories": [
                                {
                                  "name": "Test",
                                  "locations": [
                                    { "name": "Somewhere", "location": [1, 2, 3] }
                                  ]
                                }
                              ]
                            }
                            """;

        await File.WriteAllTextAsync(Path.Combine(locationsPath, "my_custom_map.json"), json);

        var locationCatalogService = new TestLocationCatalogService();
        var loader = new LocationsDataLoader(directories, locationCatalogService);

        await loader.LoadAsync();

        Assert.That(locationCatalogService.Locations, Is.Empty);
    }
}
