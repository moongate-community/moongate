using Moongate.Server.Loaders;
using Moongate.Server.Services;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

[Collection("ItemTemplateMigration")]
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

            Assert.Equal(1664, itemTemplates.Count);
            Assert.Equal(279, lootTemplates.Count);
            Assert.Equal(49, Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(140, Directory.GetFiles(lootDirectory, "*.yaml", SearchOption.AllDirectories).Length);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExecuteLoadersAsync_WhenLegacyItemFileExists_MigratesOverrideAndCustomItemBeforeLoadingLoot()
    {
        var root = NewRoot();

        try
        {
            var directories = new DirectoriesConfig(root, Array.Empty<string>());
            var dataDirectory = directories.RegisterDirectory("data");
            var legacyFile = Path.Combine(dataDirectory, "item_templates.yaml");
            var backupFile = legacyFile + ".migrated.bak";
            var itemTemplates = new ItemTemplateService();
            var lootTemplates = new LootTemplateService();
            var itemsDirectory = Path.Combine(root, "templates", "items");
            var lootDirectory = Path.Combine(root, "templates", "loot");
            var legacyApple = await LoadEmbeddedAppleAsync();
            legacyApple.Name = "Legacy Apple";
            var pipeline = new DataLoaderService(
                [
                    new ItemTemplatesLoader(itemTemplates, directories),
                    new LootTemplatesLoader(lootTemplates, itemTemplates, directories)
                ]
            );

            ItemTemplateYamlSerializer.SerializeToFile(
                legacyFile,
                [
                    legacyApple,
                    new ItemTemplate
                    {
                        Id = "custom_debug_token",
                        Name = "Custom Debug Token",
                        Category = "custom",
                        ItemId = 0x1F14,
                        Weight = 1.0,
                        IsMovable = true,
                        Rarity = ItemRarityType.Common
                    }
                ]
            );
            var legacyBytes = File.ReadAllBytes(legacyFile);

            await pipeline.ExecuteLoadersAsync();

            var customTemplate = Assert.IsType<ItemTemplate>(itemTemplates.GetById("custom_debug_token"));

            Assert.Equal(1665, itemTemplates.Count);
            Assert.Equal(279, lootTemplates.Count);
            Assert.Equal("Legacy Apple", itemTemplates.GetById("apple")!.Name);
            Assert.Equal("custom_debug_token", customTemplate.Id);
            Assert.Equal("Custom Debug Token", customTemplate.Name);
            Assert.Equal("custom", customTemplate.Category);
            Assert.Equal(0x1F14, customTemplate.ItemId);
            Assert.Equal(1.0, customTemplate.Weight);
            Assert.True(customTemplate.IsMovable);
            Assert.Equal(ItemRarityType.Common, customTemplate.Rarity);
            Assert.Equal(50, Directory.GetFiles(itemsDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(140, Directory.GetFiles(lootDirectory, "*.yaml", SearchOption.AllDirectories).Length);
            Assert.Equal(legacyBytes, File.ReadAllBytes(backupFile));
            Assert.False(File.Exists(legacyFile));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task<ItemTemplate> LoadEmbeddedAppleAsync()
    {
        var root = NewRoot();

        try
        {
            var directories = new DirectoriesConfig(root, Array.Empty<string>());
            var itemTemplates = new ItemTemplateService();

            await new ItemTemplatesLoader(itemTemplates, directories).LoadAsync();

            return itemTemplates.GetById("apple")!;
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
