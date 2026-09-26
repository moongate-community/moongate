using System.Globalization;
using System.Text.Json;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class Point2DTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new Point2DTomlConverter()]
    };

    [Fact]
    public void Deserialize_AQuotedPoint_ParsesTheCoordinates()
    {
        var holder = TomlUtils.Deserialize<Point2DHolder>("position = \"(1495, 1629)\"\n", Options);

        Assert.Equal(new(1495, 1629), holder!.Position);
    }

    [Fact]
    public void Deserialize_ANegativeCoordinate_ParsesIt()
    {
        var holder = TomlUtils.Deserialize<Point2DHolder>("position = \"(-5, 7)\"\n", Options);

        Assert.Equal(new(-5, 7), holder!.Position);
    }

    [Theory]
    [InlineData("position = \"not-a-point\"\n", "not-a-point")]
    [InlineData("position = \"(1, 2, 3)\"\n", "(1, 2, 3)")]
    [InlineData("position = \"(1)\"\n", "(1)")]
    public void Deserialize_AQuotedTextThatIsNotAPoint_ThrowsTomlException(string toml, string expectedInMessage)
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Point2DHolder>(toml, Options));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ABareInteger_ThrowsTomlException()
    {
        Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Point2DHolder>("position = 5\n", Options));
    }

    [Fact]
    public void Serialize_WritesTheQuotedText()
    {
        var toml = TomlUtils.Serialize(new Point2DHolder { Position = new(1495, 1629) }, Options);

        Assert.Equal("position = \"(1495, 1629)\"", toml.Trim());
    }

    [Fact]
    public void Serialize_UnderACultureWithAnotherMinusSign_StillWritesAnAsciiMinus()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new("sv-SE");

            var toml = TomlUtils.Serialize(new Point2DHolder { Position = new(-5, 7) }, Options);

            Assert.Equal("position = \"(-5, 7)\"", toml.Trim());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void RoundTrip_PreservesTheValue()
    {
        var original = new Point2DHolder { Position = new(-12, 3400) };

        var toml = TomlUtils.Serialize(original, Options);
        var restored = TomlUtils.Deserialize<Point2DHolder>(toml, Options);

        Assert.Equal(original.Position, restored!.Position);
    }
}
