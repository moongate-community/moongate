using Moongate.Server.Loaders;
using Moongate.Server.Services;
using Moongate.Ultima.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class ItemTemplatesLoaderTests
{
    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-items-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsAndRegistersAll()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var service = new ItemTemplateService();
        var loader = new ItemTemplatesLoader(service, directories);

        try
        {
            await loader.LoadAsync();

            Assert.True(File.Exists(Path.Combine(directories.GetPath("data"), "item_templates.yaml")));
            Assert.Equal(1664, service.Count);
            Assert.Contains(service.All, t => t.Weapon is not null && t.Equip is not null);
            Assert.Contains(service.All, t => t.Equip is not null && t.Equip.Layer != LayerType.None);
            Assert.Equal("items.training_dummy", service.GetById("training_dummy_east")!.ScriptId);
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
            Path.Combine(dataDir, "item_templates.yaml"),
            "- Id: custom\n  Name: Custom\n  ItemId: 1\n  Rarity: Rare\n  Tags: []\n"
        );
        var service = new ItemTemplateService();
        var loader = new ItemTemplatesLoader(service, directories);

        try
        {
            await loader.LoadAsync();

            Assert.Equal(1, service.Count);
            Assert.Equal("Custom", service.GetById("custom")!.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
