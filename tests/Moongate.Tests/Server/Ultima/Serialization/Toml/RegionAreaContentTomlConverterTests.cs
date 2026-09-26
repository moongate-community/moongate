using System.Text.Json;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Regions;
using Tomlyn;

namespace Moongate.Tests.Server.Ultima.Serialization.Toml;

public sealed class RegionAreaContentTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void Deserialize_MixedAreas_PreservesCornersAndOptionalHeight()
    {
        var file = TomlUtils.Deserialize<RegionContent>(
            """
            areas = [
                "(1330, 1991)..(1343, 2004)",
                { z2 = 128, bounds = "(1416, 1498)..(1740, 1777)", z1 = -10 },
                { bounds = "(1, 2)..(3, 4)", z1 = 0 },
                { x1 = 10, y1 = 20, x2 = 30, y2 = 40, z2 = -80 },
            ]
            """, Options
        )!;

        var areas = file.Areas;
        Assert.Equal(4, areas.Count);
        Assert.Equal((1330, 1991, 1343, 2004), (areas[0].X1, areas[0].Y1, areas[0].X2, areas[0].Y2));
        Assert.True(areas[0].Contains(1330, 1991, int.MinValue));
        Assert.True(areas[0].Contains(1342, 2003, int.MaxValue));
        Assert.False(areas[0].Contains(1343, 2003, 0));
        Assert.False(areas[0].Contains(1342, 2004, 0));
        Assert.True(areas[1].Contains(1416, 1498, -10));
        Assert.False(areas[1].Contains(1500, 1600, -11));
        Assert.False(areas[1].Contains(1500, 1600, 128));
        Assert.True(areas[2].Contains(1, 2, int.MaxValue));
        Assert.False(areas[2].Contains(1, 2, -1));
        Assert.True(areas[3].Contains(10, 20, -81));
        Assert.False(areas[3].Contains(10, 20, -80));
    }

    [Fact]
    public void RoundTrip_WritesCornersAndPreservesHeightLimits()
    {
        var original = new RegionContent
        {
            Areas = [
                new() { X1 = 1330, Y1 = 1991, X2 = 1343, Y2 = 2004 },
                new() { X1 = 1416, Y1 = 1498, X2 = 1740, Y2 = 1777, Z1 = -10, Z2 = 128 },
                new() { X1 = 1, Y1 = 2, X2 = 3, Y2 = 4, Z2 = -80 },
                new() { X1 = 10, Y1 = 20, X2 = 30, Y2 = 40, Z1 = 0 }
            ]
        };

        var toml = TomlUtils.Serialize(original, Options);
        Assert.Contains("\"(1330, 1991)..(1343, 2004)\"", toml);
        Assert.Contains("bounds = \"(1416, 1498)..(1740, 1777)\"", toml);
        Assert.DoesNotContain("x1 =", toml);
        var areas = TomlUtils.Deserialize<RegionContent>(toml, Options)!.Areas;
        (int X1, int Y1, int X2, int Y2, int? Z1, int? Z2)[] expected = [
            (1330, 1991, 1343, 2004, null, null),
            (1416, 1498, 1740, 1777, -10, 128),
            (1, 2, 3, 4, null, -80),
            (10, 20, 30, 40, 0, null)
        ];
        Assert.Equal(expected, areas.Select(area => (area.X1, area.Y1, area.X2, area.Y2, area.Z1, area.Z2)));
        Assert.True(areas[0].Contains(1330, 1991, int.MinValue));
        Assert.False(areas[0].Contains(1343, 1991, 0));
        Assert.Equal((-10, 128), (areas[1].Z1, areas[1].Z2));
        Assert.Null(areas[2].Z1);
        Assert.Equal(-80, areas[2].Z2);
    }

    [Theory,
     InlineData("\"(1, 2)..bad\""),
     InlineData("{ bounds = \"(1, 2)..(3, 4)\", x1 = 1 }"),
     InlineData("{ z1 = 0 }"),
     InlineData("{ x1 = 1, y1 = 2, x2 = 3 }"),
     InlineData("{ bounds = \"(1, 2)..(3, 4)\", z1 = 1.5 }"),
     InlineData("{ bounds = \"(1, 2)..(3, 4)\", z2 = 2147483648 }")]
    public void Deserialize_InvalidArea_ThrowsTomlException(string area)
    {
        Assert.Throws<TomlException>(() => TomlUtils.Deserialize<RegionContent>(
            $"areas = [{area}]\n", Options
        ));
    }
}
