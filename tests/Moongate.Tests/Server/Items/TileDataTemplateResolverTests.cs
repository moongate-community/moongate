using Moongate.Server.Services.Items;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// The client's tiledata is the authority on an item's layer and weight; the template keeps only the
/// values that deliberately diverge.
/// </summary>
[Collection("UltimaClientData")]
public class TileDataTemplateResolverTests
{
    // Kept inside 0-31: UltimaFixtures.BuildTileData() allocates exactly one 32-item old-format group.
    private const int WearableId = 4;
    private const int PlainId = 5;
    private const int UndescribedId = 900;

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

    [Fact]
    public void Resolve_WearableTileAndNoEquipBlock_SynthesisesOne()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate { Id = "book_of_bushido", ItemId = WearableId };

                Assert.True(TileDataTemplateResolver.Resolve(template));
                Assert.Equal(LayerType.Shirt, template.Equip?.Layer);
            }
        );
    }

    // An Equip block that carries only stat requirements still has a layer to learn.
    [Fact]
    public void Resolve_EquipBlockWithNoLayer_TakesTheTileLayer()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate
                {
                    Id = "hat", ItemId = WearableId, Equip = new() { StrengthReq = 10 }
                };

                Assert.True(TileDataTemplateResolver.Resolve(template));
                Assert.Equal(LayerType.Shirt, template.Equip!.Layer);
                Assert.Equal(10, template.Equip.StrengthReq);
            }
        );
    }

    [Fact]
    public void Resolve_DeclaredLayer_IsLeftAlone()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate
                {
                    Id = "bow", ItemId = WearableId, Equip = new() { Layer = LayerType.TwoHanded }
                };

                TileDataTemplateResolver.Resolve(template);

                Assert.Equal(LayerType.TwoHanded, template.Equip!.Layer);
            }
        );
    }

    [Fact]
    public void Resolve_TileTheClientDoesNotCallWearable_GainsNoEquipBlock()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate { Id = "table", ItemId = PlainId };

                TileDataTemplateResolver.Resolve(template);

                Assert.Null(template.Equip);
            }
        );
    }

    [Fact]
    public void Resolve_WeightLeftAtZero_TakesTheTileWeight()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate { Id = "shirt", ItemId = WearableId };

                Assert.True(TileDataTemplateResolver.Resolve(template));
                Assert.Equal(6.0, template.Weight);
            }
        );
    }

    [Fact]
    public void Resolve_DeclaredWeight_IsLeftAlone()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate { Id = "bandage", ItemId = WearableId, Weight = 0.1 };

                TileDataTemplateResolver.Resolve(template);

                Assert.Equal(0.1, template.Weight);
            }
        );
    }

    // Past the end of the shipped tables there is nothing to defer to.
    [Fact]
    public void Resolve_IdTheClientFilesDoNotDescribe_ChangesNothing()
    {
        WithClientFiles(
            () =>
            {
                var template = new ItemTemplate { Id = "custom", ItemId = UndescribedId };

                Assert.False(TileDataTemplateResolver.Resolve(template));
                Assert.Null(template.Equip);
                Assert.Equal(0.0, template.Weight);
            }
        );
    }

    /// <summary>
    /// Loads a tiledata.mul holding one wearable tile on the Shirt layer weighing 6, and one plain
    /// weightless tile. TileData is a process-wide static, which is why this class joins the
    /// serialized UltimaClientData collection.
    /// </summary>
    private static void WithClientFiles(Action assert)
    {
        var tileData = UltimaFixtures.BuildTileData();

        UltimaFixtures.SetItem(
            tileData,
            WearableId,
            (uint)TileFlagType.Wearable,
            0,
            "shirt",
            weight: 6,
            layer: (byte)LayerType.Shirt
        );
        UltimaFixtures.SetItem(tileData, PlainId, (uint)TileFlagType.Surface, 0, "table");

        var dir = UltimaFixtures.CreateClientDirectory(("tiledata.mul", tileData));

        try
        {
            Files.SetDirectory(dir);
            TileData.Initialize();

            assert();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
