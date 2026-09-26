using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class ItemTemplateExtensionsTests
{
    private readonly FakeTileDataService _tiles = new();

    public ItemTemplateExtensionsTests()
    {
        // 0x0EED gold: stackable (Generic), weight 1. 0x0E75 backpack: weight 3, worn on the backpack layer. 0x0FAF anvil: weight 255.
        _tiles.Item(0x0EED, TileFlagType.Generic, 0, weight: 1)
              .Item(0x0E75, TileFlagType.Container | TileFlagType.Wearable, 0, weight: 3, layer: (byte)LayerType.Backpack)
              .Item(0x0FAF, TileFlagType.None, 0, weight: 255);
    }

    [Fact]
    public void UnsetFields_FallBackToTiledata()
    {
        var gold = Template(0x0EED);
        var backpack = Template(0x0E75);
        var anvil = Template(0x0FAF);

        Assert.Equal(1m, gold.EffectiveWeight(_tiles));
        Assert.True(gold.EffectiveStackable(_tiles));
        Assert.False(backpack.EffectiveStackable(_tiles));
        Assert.Equal(LayerType.Backpack, backpack.EffectiveLayer(_tiles));
        Assert.Null(gold.EffectiveLayer(_tiles));
        Assert.True(backpack.EffectiveMovable(_tiles));
        Assert.False(anvil.EffectiveMovable(_tiles));
    }

    [Fact]
    public void SetFields_WinOverTiledata()
    {
        var coin = Template(0x0EED);
        coin.Weight = 0.02m;
        coin.Stackable = false;
        coin.Layer = LayerType.Talisman;
        coin.Movable = false;

        Assert.Equal(0.02m, coin.EffectiveWeight(_tiles));
        Assert.False(coin.EffectiveStackable(_tiles));
        Assert.Equal(LayerType.Talisman, coin.EffectiveLayer(_tiles));
        Assert.False(coin.EffectiveMovable(_tiles));
    }

    [Fact]
    public void Decay_FollowsMovableUnlessSet_WithAnHourByDefault()
    {
        Assert.True(Template(0x0E75).EffectiveDecays(_tiles));
        Assert.False(Template(0x0FAF).EffectiveDecays(_tiles));
        Assert.False(new ItemTemplate { Id = "t", ItemId = new(0x0E75), Decays = false }.EffectiveDecays(_tiles));
        Assert.Equal(TimeSpan.FromHours(1), Template(0x0E75).EffectiveDecayTime());
        Assert.Equal(TimeSpan.FromMinutes(5), new ItemTemplate { DecayMinutes = 5 }.EffectiveDecayTime());
    }

    [Fact]
    public void LootType_DefaultsToRegular()
    {
        Assert.Equal(LootType.Regular, Template(0x0E75).EffectiveLootType());
        Assert.Equal(LootType.Cursed, new ItemTemplate { LootType = LootType.Cursed }.EffectiveLootType());
    }

    private static ItemTemplate Template(uint itemId)
    {
        return new ItemTemplate { Id = "t", ItemId = new(itemId) };
    }
}
