using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.StartingItems;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Core.Yaml;

namespace Moongate.Tests.Data;

[Collection("ItemTemplateSeeding")]
public class StartingItemsDataTests
{
    [Fact]
    public void EmbeddedStartingItems_DeserializesWithContent()
    {
        var data = LoadData();

        Assert.NotEmpty(data.All.Pack);
        Assert.NotEmpty(data.ByBody);
        Assert.NotEmpty(data.BySkill);
    }

    [Fact]
    public async Task EveryReferencedItem_ResolvesToATemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-startitems-data-" + Guid.NewGuid().ToString("N"));
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var templates = new ItemTemplateService();

        try
        {
            await new ItemTemplatesLoader(templates, directories).LoadAsync();

            var data = LoadData();
            var referenced = data.All
                .Equip
                .Concat(data.All.Pack)
                .Concat(data.ByBody.Values.SelectMany(kit => kit.Equip.Concat(kit.Pack)))
                .Concat(data.BySkill.Values.SelectMany(kit => kit.Equip.Concat(kit.Pack)))
                .Select(entry => entry.Item)
                .Distinct();

            foreach (var id in referenced)
            {
                Assert.True(templates.GetById(id) is not null, $"Unknown template id: {id}");
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static StartingItemsData LoadData()
    {
        var yaml = ResourceUtils.GetEmbeddedResourceString(
            typeof(ItemTemplatesLoader).Assembly,
            "Assets/starting_items.yaml"
        );

        return YamlUtils.Deserialize<StartingItemsData>(yaml);
    }
}
