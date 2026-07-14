using Moongate.Core.Primitives;
using Moongate.Server.Services.Items;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class ItemFactoryServiceTests
{
    [Fact]
    public void Create_ClampsCountAndAmountToMinimumOne()
    {
        var items = Factory().CreateFromTemplate("dagger", 0, 0);
        var item = Assert.Single(items);
        Assert.Equal(1, item.Amount);
    }

    [Fact]
    public void CreateByCategory_ReturnsItemInCategory()
    {
        var item = Assert.Single(Factory().CreateByCategory("Clothing"));
        Assert.Equal(7939, item.ItemId);
    }

    [Fact]
    public void CreateByCategory_UnknownCategory_ReturnsEmpty()
        => Assert.Empty(Factory().CreateByCategory("Nope"));

    [Fact]
    public void CreateByTag_ReturnsCountItemsAllInTaggedSet()
    {
        var items = Factory().CreateByTag("weapon", 5);

        Assert.Equal(5, items.Count);
        Assert.All(items, i => Assert.Contains(i.ItemId, new[] { 3921, 5118 }));
    }

    [Fact]
    public void CreateByTag_UnknownTag_ReturnsEmpty()
        => Assert.Empty(Factory().CreateByTag("nope"));

    [Fact]
    public void CreateFromTemplate_AppliesHueAndAmountOverrides()
    {
        var items = Factory().CreateFromTemplate("dagger", amount: 5, hue: new Hue(1153));

        var item = Assert.Single(items);
        Assert.Equal(5, item.Amount);
        Assert.Equal((ushort)1153, item.Hue.Value);
    }

    [Fact]
    public void CreateFromTemplate_ContainerTemplateSetsGumpId()
    {
        var item = Assert.Single(Factory().CreateFromTemplate("backpack"));
        Assert.Equal(60, item.GumpId);
    }

    [Fact]
    public void CreateFromTemplate_CountProducesThatManyItems()
    {
        var items = Factory().CreateFromTemplate("dagger", 3);
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal(3921, i.ItemId));
    }

    [Fact]
    public void CreateFromTemplate_MapsFieldsAndLeavesEntityUnpersisted()
    {
        var items = Factory().CreateFromTemplate("dagger");

        var item = Assert.Single(items);
        Assert.Equal(3921, item.ItemId);
        Assert.Equal("Dagger", item.Name);
        Assert.Equal(1, item.Amount);
        Assert.Equal(Serial.Zero, item.Id);
    }

    [Fact]
    public void CreateFromTemplate_UnknownId_ReturnsEmpty()
        => Assert.Empty(Factory().CreateFromTemplate("does_not_exist"));

    private static ItemFactoryService Factory(int seed = 1)
        => new(Templates(), new(seed));

    private static ItemTemplateService Templates()
    {
        var service = new ItemTemplateService();

        service.Register(
            new()
            {
                Id = "dagger", Name = "Dagger", Category = "Weapons", ItemId = 3921,
                Hue = 0, ScriptId = "none", Rarity = ItemRarityType.Common,
                Tags = ["modernuo", "weapon"]
            }
        );
        service.Register(
            new()
            {
                Id = "katana", Name = "Katana", Category = "Weapons", ItemId = 5118,
                Tags = ["modernuo", "weapon"]
            }
        );
        service.Register(
            new()
            {
                Id = "robe", Name = "Robe", Category = "Clothing", ItemId = 7939,
                Tags = ["modernuo", "clothing"]
            }
        );
        service.Register(
            new()
            {
                Id = "backpack", Name = "Backpack", Category = "Containers", ItemId = 3701,
                Container = new() { GumpId = 60 }
            }
        );

        return service;
    }
}
