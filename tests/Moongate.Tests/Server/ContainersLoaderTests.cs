using Moongate.Server.Loaders;
using Moongate.Server.Services;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class ContainersLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-containers-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAll()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var containers = new ContainerService();
        var loader = new ContainersLoader(containers, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "containers.yaml")));
            Assert.Equal(16, containers.Count);
            Assert.Equal(3701, containers.GetById("backpack")!.ItemId);
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
            Path.Combine(dataDir, "containers.yaml"),
            "- Id: chest\n  ItemId: 100\n  Width: 5\n  Height: 3\n  Name: Chest\n"
        );
        var containers = new ContainerService();
        var loader = new ContainersLoader(containers, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, containers.Count);
            Assert.Equal("Chest", containers.GetById("chest")!.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
