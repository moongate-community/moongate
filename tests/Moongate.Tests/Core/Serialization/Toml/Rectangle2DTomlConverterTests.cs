using System.Text.Json;
using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class Rectangle2DTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new Rectangle2DTomlConverter()]
    };

    [Fact]
    public void Deserialize_TwoCorners_PreservesExclusiveEnd()
    {
        var holder = TomlUtils.Deserialize<Rectangle2DHolder>("bounds = \" (44, 65) .. (186, 159) \"\n", Options);

        Assert.Equal(new Rectangle2D(44, 65, 142, 94), holder!.Bounds);
        Assert.True(holder.Bounds.Contains(44, 65));
        Assert.False(holder.Bounds.Contains(186, 100));
        Assert.False(holder.Bounds.Contains(100, 159));
    }

    [Fact]
    public void Deserialize_AQuotedRectangle_ReadsCornerAndSize()
    {
        var holder = TomlUtils.Deserialize<Rectangle2DHolder>("bounds = \"(44, 65)+(142, 94)\"\n", Options);

        Assert.Equal(new Rectangle2D(44, 65, 142, 94), holder!.Bounds);
        Assert.Equal((142, 94), (holder.Bounds.Width, holder.Bounds.Height));
    }

    [Theory,
     InlineData("bounds = \"(44, 65)\"\n", "(44, 65)"),
     InlineData("bounds = \"44 65 142 94\"\n", "44 65 142 94"),
     InlineData("bounds = \"(44, 65)-(142, 94)\"\n", "(44, 65)-(142, 94)")]
    public void Deserialize_TextThatIsNotARectangle_ThrowsTomlException(string toml, string expectedInMessage)
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Rectangle2DHolder>(toml, Options));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ANumber_ThrowsTomlException()
    {
        Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Rectangle2DHolder>("bounds = 5\n", Options));
    }

    [Fact]
    public void RoundTrip_PreservesTheRectangle()
    {
        var original = new Rectangle2DHolder { Bounds = new(10, 20, 150, 95) };

        var toml = TomlUtils.Serialize(original, Options);

        Assert.Equal("bounds = \"(10, 20)..(160, 115)\"", toml.Trim());
        Assert.Equal(original.Bounds, TomlUtils.Deserialize<Rectangle2DHolder>(toml, Options)!.Bounds);
    }
}
