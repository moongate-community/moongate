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

    // A single stacked entry rather than 1000 entities: the factory puts the amount on one item.
    [Fact]
    public void EveryCharacter_StartsWithAThousandGold()
    {
        var entry = Assert.Single(LoadData().All.Pack.Where(item => item.Item == "gold"));

        Assert.Equal(1000, entry.Amount);
    }

    // The hues picked on the creation screen only reach the character if the body kit asks for them:
    // an entry with no Hue falls back to the template's own hue, which is 0 for every garment.
    [Theory]
    [InlineData("Human/Male", "shirt", "shirt")]
    [InlineData("Human/Male", "long_pants", "pants")]
    [InlineData("Human/Female", "fancy_shirt", "shirt")]
    [InlineData("Human/Female", "skirt", "pants")]
    [InlineData("Elf/Male", "shirt", "shirt")]
    [InlineData("Elf/Male", "long_pants", "pants")]
    [InlineData("Elf/Female", "fancy_shirt", "shirt")]
    [InlineData("Elf/Female", "skirt", "pants")]
    [InlineData("Gargoyle/Male", "robe", "shirt")]
    [InlineData("Gargoyle/Female", "robe", "shirt")]
    public void BodyKitGarments_WearThePlayerPickedHue(string body, string item, string hue)
    {
        var kit = LoadData().ByBody[body];
        var entry = Assert.Single(kit.Equip.Where(equip => equip.Item == item));

        Assert.Equal(hue, entry.Hue);
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
