using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class RegionsLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-regions-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersRegions()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var regions = new RegionService();
        var loader = new RegionsLoader(regions, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "regions.yaml")));
            Assert.Equal(398, regions.Count);
            Assert.NotEmpty(regions.ForMap(MapType.Felucca));
            Assert.Contains(regions.All, r => r.Area.Count > 0);
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
            Path.Combine(dataDir, "regions.yaml"),
            "- Type: TownRegion\n  Map: Felucca\n  Name: Britain\n  Priority: 50\n  Area:\n  - X1: 1\n    Y1: 2\n    X2: 3\n    Y2: 4\n"
        );
        var regions = new RegionService();
        var loader = new RegionsLoader(regions, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, regions.Count);
            Assert.Equal(4, regions.All[0].Area[0].Y2);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
