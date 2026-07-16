using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class StartingCitiesLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersInOrder()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var cities = new StartingCityService();
        var loader = new StartingCitiesLoader(cities, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "starting_cities.yaml")));
            Assert.Equal(9, cities.Count);
            Assert.Equal("New Haven", cities.GetByIndex(0)!.City);
            Assert.Equal(MapType.Trammel, cities.GetByIndex(0)!.Map);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPresent_LoadsExistingWithoutReseeding()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var dataDir = directories.RegisterDirectory("data");
        File.WriteAllText(
            Path.Combine(dataDir, "starting_cities.yaml"),
            "- City: Testville\n  Building: The Test Inn\n  Description: 42\n  X: 10\n  Y: 20\n  Z: 5\n  Map: Felucca\n"
        );
        var cities = new StartingCityService();
        var loader = new StartingCitiesLoader(cities, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, cities.Count);
            Assert.Equal("Testville", cities.GetByIndex(0)!.City);
            Assert.Equal(MapType.Felucca, cities.GetByIndex(0)!.Map);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-startcity-" + Guid.NewGuid().ToString("N"));
}
