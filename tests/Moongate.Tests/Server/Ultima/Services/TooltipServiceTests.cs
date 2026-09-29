using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TooltipServiceTests
{
    private readonly TooltipService _tooltips;

    public TooltipServiceTests()
    {
        var data = new StubDataLoaderService()
                   .With(
                       new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
                       new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03), Name = "robe of the magi", Weight = 2m },
                       new ItemTemplate { Id = "blessed_ring", ItemId = new Serial(0x108A), LootType = LootType.Blessed }
                   )
                   .With(
                       new MessageContent { Id = 30002, Text = "Raro" },
                       new MessageContent { Id = 30004, Text = "Leggendario" }
                   );
        var tiles = new FakeTileDataService()
                    .Item(0x0EED, TileFlagType.Generic, 0)
                    .Item(0x1F03, TileFlagType.Wearable, 0)
                    .Item(0x108A, TileFlagType.Wearable, 0)
                    .Item(0x4001, TileFlagType.None, 0);
        _tooltips = new(new ItemTemplateService(data), tiles, new LocalizationService(new LocalizationConfig { Language = "ita" }, data));
    }

    [Fact]
    public void Build_AnItemWithoutAName_UsesTheClientsClilocForItsGraphic()
    {
        var lines = _tooltips.Build(Item("gold", 0x0EED)).Entries;

        Assert.Equal((1020000 + 0x0EED, ""), (lines[0].Cliloc, lines[0].Arguments));
    }

    [Fact]
    public void Build_AGraphicFrom0x4000_UsesTheSecondClilocRange()
    {
        var lines = _tooltips.Build(Item("unknown", 0x4001)).Entries;

        Assert.Equal(1078872 + 0x4001, lines[0].Cliloc);
    }

    [Fact]
    public void Build_AStack_ShowsTheAmountAndTheClilocName()
    {
        var gold = Item("gold", 0x0EED);
        gold.Amount = 250;

        var lines = _tooltips.Build(gold).Entries;

        Assert.Equal((1050039, $"250\t#{1020000 + 0x0EED}"), (lines[0].Cliloc, lines[0].Arguments));
    }

    [Fact]
    public void Build_ATemplateName_IsShownAsText()
    {
        var lines = _tooltips.Build(Item("robe", 0x1F03)).Entries;

        Assert.Equal((1042971, "robe of the magi"), (lines[0].Cliloc, lines[0].Arguments));
    }

    [Fact]
    public void Build_AnItemsOwnName_WinsOverTheTemplates()
    {
        var robe = Item("robe", 0x1F03);
        robe.Name = "Aria's robe";

        Assert.Equal("Aria's robe", _tooltips.Build(robe).Entries[0].Arguments);
    }

    [Fact]
    public void Build_AddsTheWeightOfTheWholeStack()
    {
        var robe = Item("robe", 0x1F03);

        Assert.Contains(_tooltips.Build(robe).Entries, line => line.Cliloc == 1072789 && line.Arguments == "2");
    }

    [Theory, InlineData(LootType.Blessed, 1038021), InlineData(LootType.Newbied, 1038021), InlineData(LootType.Cursed, 1049643)]
    public void Build_TheLootTypeOfTheItem_AddsItsLine(LootType type, int cliloc)
    {
        var robe = Item("robe", 0x1F03);
        robe.SetProp(ItemPropKeys.LootType, type);

        Assert.Contains(_tooltips.Build(robe).Entries, line => line.Cliloc == cliloc);
    }

    [Fact]
    public void Build_TheTemplatesLootType_IsUsedWhenTheItemHasNone()
    {
        Assert.Contains(_tooltips.Build(Item("blessed_ring", 0x108A)).Entries, line => line.Cliloc == 1038021);
    }

    [Fact]
    public void Build_ARareItem_ShowsItsRarityTranslatedAndColoured()
    {
        var robe = Item("robe", 0x1F03);
        robe.Rarity = ItemRarityType.Legendary;

        var rarity = _tooltips.Build(robe).Entries.Single(line => line.Arguments.Contains("Leggendario"));

        Assert.StartsWith("<BASEFONT COLOR=#", rarity.Arguments);
    }

    [Fact]
    public void Build_ACommonItem_ShowsNoRarity()
    {
        Assert.DoesNotContain(_tooltips.Build(Item("robe", 0x1F03)).Entries, line => line.Arguments.Contains("BASEFONT"));
    }

    [Fact]
    public void Build_AMobile_ShowsNameAndTitle()
    {
        var mage = new MobileEntity { Id = new(0x00000010), Name = "Nystul", Title = "the mage" };
        var guard = new MobileEntity { Id = new(0x00000011), Name = "a guard" };

        Assert.Equal((1050045, " \tNystul\t the mage"), Line(_tooltips.Build(mage)));
        Assert.Equal((1050045, " \ta guard\t "), Line(_tooltips.Build(guard)));
    }

    private static (int, string) Line(Moongate.Server.Ultima.Data.Tooltips.PropertyList list)
    {
        var line = Assert.Single(list.Entries);

        return (line.Cliloc, line.Arguments);
    }

    private static ItemEntity Item(string template, int graphic)
    {
        return new() { Id = new(0x40000010), TemplateId = template, ItemId = graphic, Amount = 1 };
    }
}
