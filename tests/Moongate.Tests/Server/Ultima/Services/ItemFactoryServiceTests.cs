using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemFactoryServiceTests
{
    private readonly ItemFactoryService _factory;

    public ItemFactoryServiceTests()
    {
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03), Hue = HueSpec.FromRange(2, 5), Name = "robe", Movable = true },
                new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED), Amount = RangeValueSpec<int>.FromRange(10, 20) },
                new ItemTemplate { Id = "bad_pile", ItemId = new Serial(0x1F03), Amount = RangeValueSpec<int>.FromValue(3) },
                new ItemTemplate
                {
                    Id = "gem",
                    ItemId = new Serial(0x0F10),
                    Rarity = EnumValueSpec<ItemRarityType>.FromCandidates([ItemRarityType.Rare, ItemRarityType.Epic])
                }
            )
        );
        var tiles = new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0)
                                             .Item(0x1F03, TileFlagType.None, 0)
                                             .Item(0x0F10, TileFlagType.None, 0);

        // Create never touches persistence; the integration tests cover saving.
        _factory = new ItemFactoryService(templates, tiles, null!);
    }

    [Fact]
    public void Create_ResolvesTheTemplateOnce_AndCopiesNothingElse()
    {
        var robe = _factory.Create("robe");

        Assert.Equal((Serial.Zero, "robe", 0x1F03, 1), (robe.Id, robe.TemplateId, robe.ItemId, robe.Amount));
        Assert.InRange(robe.Hue.Value, 2, 5);
        Assert.Null(robe.Name);
        Assert.Null(robe.Movable);
        Assert.Null(robe.Visibility);
        Assert.Null(robe.Props);
        Assert.Equal(ItemLocationType.None, robe.Location);
    }

    [Fact]
    public void Create_Overrides_AndRandomAmountAndRarity()
    {
        var gold = _factory.Create("gold", 250, new Hue(0x455));

        Assert.Equal((250, (ushort)0x455), (gold.Amount, gold.Hue.Value));
        Assert.InRange(_factory.Create("gold").Amount, 10, 20);
        Assert.Contains(_factory.Create("gem").Rarity, new[] { ItemRarityType.Rare, ItemRarityType.Epic });
    }

    [Fact]
    public void Create_BadInput_Throws()
    {
        Assert.Contains("'cape'", Assert.Throws<KeyNotFoundException>(() => _factory.Create("cape")).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.Create("gold", 0));
        Assert.Throws<ArgumentException>(() => _factory.Create("robe", 4));
        Assert.Throws<ArgumentException>(() => _factory.Create("bad_pile"));
    }
}
