using Moongate.Server.Services.Items;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class LootServiceTests
{
    [Fact]
    public void Roll_Additive_ChanceOne_DropsAllEntriesEachRoll()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 2,
                Entries =
                [
                    new() { ItemTemplateId = "gold", Amount = 1, Chance = 1.0 },
                    new() { ItemTemplateId = "gold", Amount = 1, Chance = 1.0 }
                ]
            }
        );

        Assert.Equal(4, Service(loot).Roll("t").Count);
    }

    [Fact]
    public void Roll_Additive_ChanceZero_ReturnsEmpty()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 3,
                Entries = [new() { ItemTemplateId = "gold", Chance = 0.0 }]
            }
        );

        Assert.Empty(Service(loot).Roll("t"));
    }

    [Fact]
    public void Roll_AmountRange_StaysWithinBounds()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 20,
                Entries = [new() { ItemTemplateId = "gold", AmountMin = 5, AmountMax = 15, Chance = 1.0 }]
            }
        );

        Assert.All(Service(loot).Roll("t"), item => Assert.InRange(item.Amount, 5, 15));
    }

    [Fact]
    public void Roll_EntryWithoutItemOrTag_Skips()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 1,
                Entries = [new() { Amount = 1, Chance = 1.0 }]
            }
        );

        Assert.Empty(Service(loot).Roll("t"));
    }

    [Fact]
    public void Roll_ItemTag_CreatesTaggedItem()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Additive,
                Rolls = 1,
                Entries = [new() { ItemTag = "healing", Amount = 1, Chance = 1.0 }]
            }
        );

        var item = Assert.Single(Service(loot).Roll("t"));
        Assert.Equal(3617, item.ItemId);
    }

    [Fact]
    public void Roll_UnknownTable_ReturnsEmpty()
        => Assert.Empty(Service(new()).Roll("nope"));

    [Fact]
    public void Roll_Weighted_OnlyNoDrop_ReturnsEmpty()
    {
        var loot = new LootTemplateService();
        loot.Register(new() { Id = "t", Mode = LootTemplateModeType.Weighted, Rolls = 5, NoDropWeight = 1 });

        Assert.Empty(Service(loot).Roll("t"));
    }

    [Fact]
    public void Roll_Weighted_SingleEntry_DropsEveryRoll()
    {
        var loot = new LootTemplateService();
        loot.Register(
            new()
            {
                Id = "t",
                Mode = LootTemplateModeType.Weighted,
                Rolls = 3,
                Entries = [new() { Weight = 1, ItemTemplateId = "gold", Amount = 10 }]
            }
        );

        var items = Service(loot).Roll("t");

        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.Equal(3821, item.ItemId));
        Assert.All(items, item => Assert.Equal(10, item.Amount));
    }

    private static LootService Service(LootTemplateService loot)
    {
        var itemTemplates = new ItemTemplateService();
        itemTemplates.Register(new() { Id = "gold", Name = "Gold", Category = "Currency", ItemId = 3821 });
        itemTemplates.Register(
            new() { Id = "bandage", Name = "Bandage", Category = "Healing", ItemId = 3617, Tags = { "healing" } }
        );

        var itemFactory = new ItemFactoryService(itemTemplates, new(1));

        return new(loot, itemFactory, new(1));
    }
}
