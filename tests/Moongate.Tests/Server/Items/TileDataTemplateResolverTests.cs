using Moongate.Server.Services.Items;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// The client's tiledata is the authority on an item's layer and weight; the template keeps only the
/// values that deliberately diverge.
/// </summary>
public class TileDataTemplateResolverTests
{
    // ModernUO's Item.DefaultWeight reads both 0 and the 255 immovable sentinel as "no usable
    // weight" and substitutes 1, rather than claiming a 255-stone item.
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(255, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(6, 6.0)]
    [InlineData(200, 200.0)]
    public void WeightFor_ZeroAndTheImmovableSentinel_BecomeOne(int tileWeight, double expected)
    {
        Assert.Equal(expected, TileDataTemplateResolver.WeightFor(tileWeight));
    }

    [Fact]
    public void LayerFor_WearableTile_ReadsTheByteAsALayer()
    {
        Assert.Equal(LayerType.Shirt, TileDataTemplateResolver.LayerFor(wearable: true, layerByte: 5));
    }

    [Fact]
    public void LayerFor_TileTheClientDoesNotCallWearable_HasNoLayer()
    {
        Assert.Null(TileDataTemplateResolver.LayerFor(wearable: false, layerByte: 5));
    }

    [Fact]
    public void LayerFor_WearableTileWithNoLayerByte_HasNoLayer()
    {
        Assert.Null(TileDataTemplateResolver.LayerFor(wearable: true, layerByte: 0));
    }

    // Nothing guarantees the byte lands inside LayerType; a value we cannot name is not a layer.
    [Fact]
    public void LayerFor_ByteOutsideTheEnum_HasNoLayer()
    {
        Assert.Null(TileDataTemplateResolver.LayerFor(wearable: true, layerByte: 200));
    }
}
