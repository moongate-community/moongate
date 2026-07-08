using Moongate.Server.Loaders;
using Moongate.Server.Services;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class SignsLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-signs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersSigns()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var signs = new SignService();
        var loader = new SignsLoader(signs, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "signs.yaml")));
            Assert.Equal(509, signs.Count);
            Assert.NotEmpty(signs.ForMap(MapType.Felucca));
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
            Path.Combine(dataDir, "signs.yaml"),
            "- Map: Malas\n  ItemId: 3032\n  X: 10\n  Y: 20\n  Z: -1\n  Label: '#1016093'\n"
        );
        var signs = new SignService();
        var loader = new SignsLoader(signs, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, signs.Count);
            Assert.Equal(-1, signs.All[0].Z);
            Assert.Equal(MapType.Malas, signs.All[0].Map);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
