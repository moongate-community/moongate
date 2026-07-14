using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Loading;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

[Collection("ItemTemplateSeeding")]
public class ItemLootDataLoaderPipelineTests
{
    [Fact]
    public async Task ExecuteLoadersAsync_WhenRootIsClean_SeedsCompleteItemAndLootTrees()
    {
        var root = NewRoot();

        try
        {
            var directories = new DirectoriesConfig(root, Array.Empty<string>());
            var itemTemplates = new ItemTemplateService();
            var lootTemplates = new LootTemplateService();
            var itemsDirectory = Path.Combine(root, "templates", "items");
            var lootDirectory = Path.Combine(root, "templates", "loot");
            var pipeline = new DataLoaderService(
                [
                    new ItemTemplatesLoader(itemTemplates, directories),
                    new LootTemplatesLoader(lootTemplates, itemTemplates, directories)
                ]
            );

            await pipeline.ExecuteLoadersAsync();

            Assert.Equal(1665, itemTemplates.Count);
            Assert.Equal(279, lootTemplates.Count);
            Assert.Equal(49, Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(140, Directory.GetFiles(lootDirectory, "*.yaml", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
    {
        return Path.Combine(Path.GetTempPath(), "mg-item-loot-pipeline-" + Guid.NewGuid().ToString("N"));
    }
}
