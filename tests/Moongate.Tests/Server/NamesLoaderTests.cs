using Moongate.Server.Loaders;
using Moongate.Server.Services;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class NamesLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-names-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAllLists()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var names = new NameService();
        var loader = new NamesLoader(names, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "names.yaml")));
            Assert.Equal(28, names.Count);
            Assert.NotEmpty(names.GetByType("orc")!.Names);
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
            Path.Combine(dataDir, "names.yaml"),
            "- Type: goblin\n  Names:\n  - Grik\n  - Snor\n"
        );
        var names = new NameService();
        var loader = new NamesLoader(names, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, names.Count);
            Assert.Equal(2, names.GetByType("goblin")!.Names.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
