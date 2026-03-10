using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Containers;

namespace Moongate.Tests.Server.FileLoaders;

public class ContainersDataLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenJsonAndCfgPresent_ShouldMergeContainerDefinitions()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var containersPath = Path.Combine(directories[DirectoryType.Data], "containers");
        Directory.CreateDirectory(containersPath);

        const string json = """
                            [
                              { "Id": "bag", "ItemId": 3708, "Width": 6, "Height": 6, "Name": "Bag" }
                            ]
                            """;
        await File.WriteAllTextAsync(Path.Combine(containersPath, "default_containers.json"), json);

        const string cfg = """
                           # Default:
                           0x3C	44 65 142 94	0x48
                           # Containers
                           0x4A	18 105 144 73	0x42	0xE7C,0x9AB
                           """;
        await File.WriteAllTextAsync(Path.Combine(containersPath, "containers.cfg"), cfg);

        var loader = new ContainersDataLoader(directories);
        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(ContainerLayoutSystem.ContainerSizesById.TryGetValue("bag", out var size), Is.True);
                Assert.That(size!.Width, Is.EqualTo(6));
                Assert.That(size.Height, Is.EqualTo(6));

                Assert.That(ContainerLayoutSystem.ContainerBagDefsById.TryGetValue("bag", out var defById), Is.True);
                Assert.That(defById!.ItemId, Is.EqualTo(3708));
                Assert.That(defById.GumpId, Is.EqualTo(0x4A));
                Assert.That(defById.DropSound, Is.EqualTo(0x42));
                Assert.That(defById.Bounds.X, Is.EqualTo(18));
                Assert.That(defById.Bounds.Y, Is.EqualTo(105));
                Assert.That(defById.Bounds.Width, Is.EqualTo(144));
                Assert.That(defById.Bounds.Height, Is.EqualTo(73));
            }
        );
    }

    [Test]
    public async Task LoadAsync_WhenCfgContainsUnknownItemId_ShouldCreateFallbackDefinition()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var containersPath = Path.Combine(directories[DirectoryType.Data], "containers");
        Directory.CreateDirectory(containersPath);

        const string cfg = """
                           0x4A	18 105 144 73	0x42	0xE7C
                           """;
        await File.WriteAllTextAsync(Path.Combine(containersPath, "containers.cfg"), cfg);

        var loader = new ContainersDataLoader(directories);
        await loader.LoadAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(ContainerLayoutSystem.ContainerBagDefsByItemId.TryGetValue(0x0E7C, out var def), Is.True);
                Assert.That(def!.Id, Is.EqualTo("item_0e7c"));
                Assert.That(def.GumpId, Is.EqualTo(0x4A));
                Assert.That(def.DropSound, Is.EqualTo(0x42));
                Assert.That(ContainerLayoutSystem.ContainerSizesById.TryGetValue("item_0e7c", out var size), Is.True);
                Assert.That(size!.Width, Is.EqualTo(7));
                Assert.That(size.Height, Is.EqualTo(4));
            }
        );
    }
}
