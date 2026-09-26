using Moongate.Server.Ultima.Services;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class TileDataServiceTests
{
    [Fact]
    public void GetItem_LoadedTable_CopiesEveryField()
    {
        var service = CreateService();

        var backpack = service.GetItem(1);

        Assert.Equal(1, backpack.Id);
        Assert.Equal("backpack", backpack.Name);
        Assert.Equal(TileFlagType.Container | TileFlagType.Wearable, backpack.Flags);
        Assert.Equal((3, 2, 2), (backpack.Weight, backpack.Height, backpack.StandHeight));
        Assert.Equal((21, 1, 0x3C), (backpack.Layer, backpack.Quantity, backpack.Animation));
    }

    [Fact]
    public void GetItem_Bridge_StandsOnHalfTheHeight()
    {
        var stair = CreateService().GetItem(2);

        Assert.Equal((10, 5), (stair.Height, stair.StandHeight));
    }

    [Fact]
    public void GetLand_LoadedTable_CopiesEveryField()
    {
        var water = CreateService().GetLand(1);

        Assert.Equal(1, water.Id);
        Assert.Equal("water", water.Name);
        Assert.Equal(TileFlagType.Wet | TileFlagType.Impassable, water.Flags);
        Assert.Equal(7, water.TextureId);
    }

    [Theory, InlineData(-1), InlineData(3)]
    public void TryGetItemAndLand_OutOfRange_ReturnFalse(int id)
    {
        var service = CreateService();

        Assert.False(service.TryGetItem(id, out _));
        Assert.False(service.TryGetLand(id, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetItem(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetLand(id));
    }

    [Fact]
    public void Counts_LoadedTable_MatchTheTables()
    {
        var service = CreateService();

        Assert.Equal((2, 3), (service.LandCount, service.ItemCount));
    }

    [Fact]
    public void GetItem_SourceChangedAfterFirstUse_KeepsTheCopy()
    {
        var items = Items();
        var service = new TileDataService(Land, () => items);

        _ = service.GetItem(1);
        items[1].Name = "changed";

        Assert.Equal("backpack", service.GetItem(1).Name);
    }

    [Fact]
    public void GetItem_TablesNotLoaded_ThrowsInvalidOperationException()
    {
        var service = new TileDataService(() => null, () => null);

        var exception = Assert.Throws<InvalidOperationException>(() => service.GetItem(0));

        Assert.Contains("tiledata.mul", exception.Message);
    }

    private static TileDataService CreateService()
    {
        return new(Land, Items);
    }

    private static LandData[] Land()
    {
        return
        [
            new() { Name = "grass" },
            new() { Name = "water", Flags = TileFlagType.Wet | TileFlagType.Impassable, TextureId = 7 }
        ];
    }

    private static ItemData[] Items()
    {
        return
        [
            new() { Name = "nothing" },
            new()
            {
                Name = "backpack",
                Flags = TileFlagType.Container | TileFlagType.Wearable,
                Weight = 3,
                Height = 2,
                Quality = 21,
                Quantity = 1,
                Animation = 0x3C
            },
            new() { Name = "stairs", Flags = TileFlagType.Bridge | TileFlagType.Surface, Height = 10 }
        ];
    }
}
