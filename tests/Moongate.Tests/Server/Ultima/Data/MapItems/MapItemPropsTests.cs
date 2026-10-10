using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Tests.Server.Ultima.Data.MapItems;

public sealed class MapItemPropsTests
{
    private readonly ItemEntity _map = new() { Id = new Serial(0x40000001), TemplateId = "britainmap", ItemId = 0x14EC, Amount = 1 };

    private readonly ItemTemplate _britain = new()
    {
        Id = "britainmap",
        Tags = new()
        {
            ["map_x1"] = "1092", ["map_y1"] = "1396", ["map_x2"] = "1736", ["map_y2"] = "1924", ["map_width"] = "200",
            ["map_height"] = "200"
        }
    };

    [Fact]
    public void TryGetArea_OfAPresetTemplate_ReadsItsTags_OnFelucca()
    {
        Assert.True(MapItemProps.TryGetArea(_map, _britain, out var area));

        Assert.Equal(new MapArea(1092, 1396, 1736, 1924, 200, 200, 0), area);
    }

    [Fact]
    public void TryGetArea_PropsOfTheItem_WinOverTheTemplate()
    {
        MapItemProps.SetArea(_map, new(10, 20, 110, 120, 300, 300, 1));

        Assert.True(MapItemProps.TryGetArea(_map, _britain, out var area));
        Assert.Equal(new MapArea(10, 20, 110, 120, 300, 300, 1), area);
    }

    [Fact]
    public void TryGetArea_OfABlankMap_IsFalse()
    {
        Assert.False(MapItemProps.TryGetArea(_map, new ItemTemplate { Id = "blank" }, out _));
        Assert.False(MapItemProps.TryGetArea(_map, null, out _));
    }

    [Fact]
    public void SetArea_KeepsTheCornersInTheWorld_AndTheSizeDrawable()
    {
        MapItemProps.SetArea(_map, new(-5, -5, 9000, 9000, 0, 5000, 0));

        Assert.True(MapItemProps.TryGetArea(_map, null, out var area));
        Assert.Equal(new MapArea(0, 0, 5119, 4095, 1, 800, 0), area);
    }

    [Fact]
    public void Pins_GoAndComeBack_AndAnEmptyCourseRemovesTheProp()
    {
        MapItemProps.SetPins(_map, [(10, 20), (30, 40)]);

        Assert.Equal([(10, 20), (30, 40)], MapItemProps.GetPins(_map));

        MapItemProps.SetPins(_map, []);
        Assert.False(_map.TryGetProp<string>(MapItemProps.PinsProp, out _));
        Assert.Empty(MapItemProps.GetPins(_map));
    }

    [Fact]
    public void GetPins_OfCorruptText_IsAnEmptyCourse()
    {
        _map.SetProp(MapItemProps.PinsProp, "10,20;oops");

        Assert.Empty(MapItemProps.GetPins(_map));
    }

    [Fact]
    public void EditableAndProtected_AreFalseUntilSet()
    {
        Assert.False(MapItemProps.IsEditable(_map));
        Assert.False(MapItemProps.IsProtected(_map));

        MapItemProps.SetEditable(_map, true);
        MapItemProps.SetProtected(_map, true);

        Assert.True(MapItemProps.IsEditable(_map));
        Assert.True(MapItemProps.IsProtected(_map));
    }

    [Fact]
    public void TryGetArea_KeepsAnAreaSetByHandDrawable()
    {
        MapItemProps.SetArea(_map, new(10, 20, 110, 120, 300, 300, 1));
        _map.SetProp(MapItemProps.WidthProp, 70000L);
        _map.SetProp(MapItemProps.X2Prop, -4L);

        Assert.True(MapItemProps.TryGetArea(_map, null, out var area));
        Assert.Equal((0, 800), (area.X2, area.Width));
    }

    [Fact]
    public void WorldToPixel_OfTheFarEdge_StaysOnTheDrawing()
    {
        var area = new MapArea(100, 100, 300, 300, 200, 200, 0);

        Assert.Equal((199, 199), MapItemProps.WorldToPixel(area, 300, 300));
    }

    [Theory]
    [InlineData(2, 2303, 1599)]
    [InlineData(3, 2559, 2047)]
    [InlineData(4, 1447, 1447)]
    [InlineData(5, 1279, 4095)]
    [InlineData(1, 5119, 4095)]
    public void Clamp_KeepsTheAreaInsideItsOwnFacet(int facet, int lastX, int lastY)
    {
        var area = MapItemProps.Clamp(new(-10, -10, 9000, 9000, 200, 200, facet));

        Assert.Equal((0, 0, lastX, lastY), (area.X1, area.Y1, area.X2, area.Y2));
    }

    [Fact]
    public void WorldToPixel_ScalesTheTileIntoTheDrawing()
    {
        var world = new MapArea(0, 0, 5120, 4096, 200, 200, 0);

        Assert.Equal((100, 50), MapItemProps.WorldToPixel(world, 2560, 1024));
    }
}
