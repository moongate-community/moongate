using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TooltipServiceTests
{
    private static readonly Serial Aria = new(0x00000002);
    private static readonly Serial Bran = new(0x00000003);

    private readonly TooltipService _tooltips;
    private readonly ItemService _items;
    private readonly MobileService _mobiles;

    public TooltipServiceTests()
    {
        var data = new StubDataLoaderService()
                   .With(
                       new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED) },
                       new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03), Name = "robe of the magi", Weight = 2m },
                       new ItemTemplate { Id = "blessed_ring", ItemId = new Serial(0x108A), LootType = LootType.Blessed },
                       new ItemTemplate { Id = "feather", ItemId = new Serial(0x1BD1), Weight = 0.1m },
                       new ItemTemplate { Id = "statue", ItemId = new Serial(0x1224), Movable = false }
                   )
                   .With(
                       new MessageContent { Id = 30000, Text = "Comune" },
                       new MessageContent { Id = 30002, Text = "Raro" },
                       new MessageContent { Id = 30004, Text = "Leggendario" }
                   );
        var tiles = new FakeTileDataService()
                    .Item(0x0EED, TileFlagType.Generic, 0)
                    .Item(0x1F03, TileFlagType.Wearable, 0)
                    .Item(0x108A, TileFlagType.Wearable, 0)
                    .Item(0x4001, TileFlagType.None, 0);
        var sectors = TestSectors.Create();
        _items = TestItems.Create(sectors);
        _mobiles = new(new StubMovementService(), sectors);
        _mobiles.EnterWorld(new() { Id = Aria, Name = "Aria", Map = MapType.Trammel, Location = new Point3D(1000, 1000, 0) });
        _mobiles.EnterWorld(new() { Id = Bran, Name = "Bran", Map = MapType.Trammel, Location = new Point3D(1010, 1000, 0) });
        _tooltips = new(
            new ItemTemplateService(data),
            tiles,
            new LocalizationService(new LocalizationConfig { Language = "ita" }, data),
            _items,
            _mobiles,
            new WorldConfig()
        );
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

    [Fact]
    public void Build_ALightItem_WeighsAtLeastOneStone()
    {
        // As ModernUO's PileWeight: the weight is rounded up.
        Assert.Contains(_tooltips.Build(Item("feather", 0x1BD1)).Entries, line => line.Cliloc == 1072788 && line.Arguments == "1");
    }

    [Fact]
    public void Build_AnImmovableItem_ShowsNoWeight()
    {
        Assert.DoesNotContain(_tooltips.Build(Item("statue", 0x1224)).Entries, line => line.Cliloc is 1072788 or 1072789);
    }

    [Fact]
    public void Build_AnItemMadeImmovable_ShowsNoWeight()
    {
        var robe = Item("robe", 0x1F03);
        robe.Movable = false;

        Assert.DoesNotContain(_tooltips.Build(robe).Entries, line => line.Cliloc is 1072788 or 1072789);
    }

    [Fact]
    public void Build_ARarityWithoutAMessage_ShowsItsEnglishName()
    {
        // The message files have no text for Epic here: the tooltip must not throw.
        var robe = Item("robe", 0x1F03);
        robe.Rarity = ItemRarityType.Epic;

        Assert.Contains(_tooltips.Build(robe).Entries, line => line.Arguments.Contains(">Epic<"));
    }

    [Fact]
    public void Build_ANameWithATab_KeepsItInOneArgument()
    {
        var robe = Item("robe", 0x1F03);
        robe.Name = "a\tb";
        robe.Amount = 2;

        Assert.Equal("2\ta b", _tooltips.Build(robe).Entries[0].Arguments);
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
    public void Build_ACommonItem_ShowsItsRarityInWhite()
    {
        Assert.Contains(
            _tooltips.Build(Item("robe", 0x1F03)).Entries,
            line => line.Arguments == "<BASEFONT COLOR=#FFFFFF>Comune</BASEFONT>"
        );
    }

    [Fact]
    public void Build_AMobile_ShowsNameAndTitle()
    {
        var mage = new MobileEntity { Id = new(0x00000010), Name = "Nystul", Title = "the mage" };
        var guard = new MobileEntity { Id = new(0x00000011), Name = "a guard" };

        Assert.Equal((1050045, " \tNystul\t the mage"), Line(_tooltips.Build(mage)));
        Assert.Equal((1050045, " \ta guard\t "), Line(_tooltips.Build(guard)));
    }

    [Fact]
    public void Build_TwoItemsWithTheSameContent_ShareOneCachedTooltip()
    {
        var first = Item("robe", 0x1F03);
        var second = Item("robe", 0x1F03);
        second.Id = new(0x40000011);

        Assert.Same(_tooltips.Build(first), _tooltips.Build(second));
    }

    [Fact]
    public void Build_AChangedItem_GetsANewTooltip()
    {
        var gold = Item("gold", 0x0EED);
        var before = _tooltips.Build(gold);

        gold.Amount = 7;

        var after = _tooltips.Build(gold);
        Assert.NotSame(before, after);
        Assert.Equal($"7\t#{1020000 + 0x0EED}", after.Entries[0].Arguments);
    }

    [Fact]
    public void Build_AChangedMobile_GetsANewTooltip()
    {
        var mage = new MobileEntity { Id = new(0x00000010), Name = "Nystul" };
        var before = _tooltips.Build(mage);

        mage.Title = "the mage";

        Assert.NotSame(before, _tooltips.Build(mage));
        Assert.Same(_tooltips.Build(mage), _tooltips.Build(mage));
    }

    [Fact]
    public void Build_AFullCache_StartsOver()
    {
        var gold = Item("gold", 0x0EED);
        var first = _tooltips.Build(gold);

        for (var amount = 2; amount <= TooltipService.MaxCachedTooltips + 1; amount++)
        {
            gold.Amount = amount;
            _tooltips.Build(gold);
        }

        gold.Amount = 1;
        Assert.NotSame(first, _tooltips.Build(gold));
    }

    [Fact]
    public void Info_CarriesTheSerialAndTheHashOfTheTooltip()
    {
        var robe = Item("robe", 0x1F03);

        var info = _tooltips.Info(robe);

        Assert.Equal((robe.Id, _tooltips.Build(robe).Hash), (info.Serial, info.Hash));
    }

    [Fact]
    public void TryBuildFor_AnItemTheCharacterCarries_IsBuilt()
    {
        var backpack = Placed(0x40000001, item => item.Equip(Aria, LayerType.Backpack));
        var coin = Placed(0x40000002, item => item.PutInContainer(backpack.Id, new Point2D(44, 65)));

        Assert.True(_tooltips.TryBuildFor(Aria, coin.Id, out var list));
        Assert.NotEmpty(list.Entries);
    }

    [Fact]
    public void TryBuildFor_AnItemInAnotherCharactersBackpack_IsRefused()
    {
        var backpack = Placed(0x40000001, item => item.Equip(Bran, LayerType.Backpack));
        var coin = Placed(0x40000002, item => item.PutInContainer(backpack.Id, new Point2D(44, 65)));

        Assert.False(_tooltips.TryBuildFor(Aria, coin.Id, out _));
    }

    [Fact]
    public void TryBuildFor_AWornItemOfAMobileInRange_IsBuilt()
    {
        var shirt = Placed(0x40000003, item => item.Equip(Bran, LayerType.Shirt));

        Assert.True(_tooltips.TryBuildFor(Aria, shirt.Id, out _));
    }

    [Fact]
    public void TryBuildFor_AGroundItem_IsBuiltOnlyInViewRange()
    {
        var near = Placed(0x40000004, item => { });
        _items.PlaceOnGround(near, MapType.Trammel, new Point3D(1005, 1000, 0));
        var far = Placed(0x40000005, item => { });
        _items.PlaceOnGround(far, MapType.Trammel, new Point3D(1100, 1000, 0));

        Assert.True(_tooltips.TryBuildFor(Aria, near.Id, out _));
        Assert.False(_tooltips.TryBuildFor(Aria, far.Id, out _));
    }

    [Fact]
    public void TryBuildFor_AMobile_IsBuiltOnlyInViewRangeOnTheSameMap()
    {
        var elsewhere = new MobileEntity { Id = new(0x00000004), Name = "Far", Map = MapType.Felucca, Location = new Point3D(1000, 1000, 0) };
        _mobiles.EnterWorld(elsewhere);

        Assert.True(_tooltips.TryBuildFor(Aria, Bran, out var list));
        Assert.Equal(1050045, list.Entries[0].Cliloc);
        Assert.True(_tooltips.TryBuildFor(Aria, Aria, out _));
        Assert.False(_tooltips.TryBuildFor(Aria, elsewhere.Id, out _));
    }

    [Fact]
    public void TryBuildFor_AnUnknownSerial_IsRefused()
    {
        Assert.False(_tooltips.TryBuildFor(Aria, new Serial(0x40009999), out _));
    }

    private ItemEntity Placed(uint serial, Action<ItemEntity> place)
    {
        var item = new ItemEntity { Id = new(serial), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        place(item);
        _items.Add([item]);

        return item;
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
