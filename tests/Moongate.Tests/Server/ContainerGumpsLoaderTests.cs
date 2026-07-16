using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class ContainerGumpsLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAll()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var gumps = new ContainerGumpService();
        var loader = new ContainerGumpsLoader(gumps, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "container_gumps.yaml")));
            Assert.Equal(32, gumps.Count);

            // gump 0x9 (=9) maps item 0x2006 (=8198)
            Assert.Equal(9, gumps.GetByItemId(8198)!.GumpId);
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
            Path.Combine(dataDir, "container_gumps.yaml"),
            "- GumpId: 61\n  RectX: 0\n  RectY: 0\n  RectWidth: 282\n  RectHeight: 210\n  DropSound: -1\n  ItemIds:\n  - 4006\n"
        );
        var gumps = new ContainerGumpService();
        var loader = new ContainerGumpsLoader(gumps, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, gumps.Count);
            Assert.Equal(-1, gumps.GetByGumpId(61)!.DropSound);
            Assert.Equal(61, gumps.GetByItemId(4006)!.GumpId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-cgumps-" + Guid.NewGuid().ToString("N"));
}
