using Moongate.Server.Loaders;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class TeleportersLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersTeleporters()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var teleporters = new TeleporterService();
        var loader = new TeleportersLoader(teleporters, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "teleporters.yaml")));
            Assert.Equal(1368, teleporters.Count);
            Assert.NotEmpty(teleporters.ForMap(MapType.Felucca));
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
            Path.Combine(dataDir, "teleporters.yaml"),
            "- Src:\n    Map: Felucca\n    X: 10\n    Y: 20\n    Z: 0\n  Dst:\n    Map: Malas\n    X: 30\n    Y: 40\n    Z: 5\n  Back: true\n"
        );
        var teleporters = new TeleporterService();
        var loader = new TeleportersLoader(teleporters, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, teleporters.Count);
            Assert.True(teleporters.All[0].Back);
            Assert.Equal(MapType.Malas, teleporters.All[0].Dst.Map);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-tele-" + Guid.NewGuid().ToString("N"));
}
