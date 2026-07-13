using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Locations;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class LocationsLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-loc-" + Guid.NewGuid().ToString("N"));

    private static IEnumerable<LocationEntry> AllEntries(LocationCategory node)
    {
        foreach (var entry in node.Locations)
        {
            yield return entry;
        }

        foreach (var child in node.Categories)
        {
            foreach (var entry in AllEntries(child))
            {
                yield return entry;
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAllFacets()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var locations = new LocationService();
        var loader = new LocationsLoader(locations, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(6, locations.Count);
            var felucca = locations.GetFacet("Felucca")!;
            Assert.NotEmpty(felucca.Categories);
            var britain = AllEntries(felucca).First(e => e.Name == "Britain" && e.X == 1592);
            Assert.Equal(1680, britain.Y);
            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "locations", "felucca.yaml")));
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
        var locDir = Directory.CreateDirectory(Path.Combine(dataDir, "locations")).FullName;
        File.WriteAllText(
            Path.Combine(locDir, "felucca.yaml"),
            "Name: Felucca\nLocations:\n- Name: Britain\n  X: 1592\n  Y: 1680\n  Z: 10\n"
        );

        var locations = new LocationService();
        var loader = new LocationsLoader(locations, directories);

        try
        {
            await loader.LoadAsync();

            // The 5 other facets seed from embedded resources; felucca loads the existing file.
            Assert.Equal(6, locations.Count);
            Assert.Single(locations.GetFacet("Felucca")!.Locations);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
