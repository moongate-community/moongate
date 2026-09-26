using System.Globalization;
using System.Text.Json;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class Point3DTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new Point3DTomlConverter()]
    };

    [Fact]
    public void Deserialize_AQuotedPoint_ParsesTheCoordinates()
    {
        var holder = TomlUtils.Deserialize<Point3DHolder>("location = \"(1495, 1629, 10)\"\n", Options);

        Assert.Equal(new(1495, 1629, 10), holder!.Location);
    }

    [Fact]
    public void Deserialize_ANegativeCoordinate_ParsesIt()
    {
        var holder = TomlUtils.Deserialize<Point3DHolder>("location = \"(-5, 7, -20)\"\n", Options);

        Assert.Equal(new(-5, 7, -20), holder!.Location);
    }

    [Theory]
    [InlineData("location = \"not-a-point\"\n", "not-a-point")]
    [InlineData("location = \"(1, 2)\"\n", "(1, 2)")]
    [InlineData("location = \"(1, 2, 3, 4)\"\n", "(1, 2, 3, 4)")]
    public void Deserialize_AQuotedTextThatIsNotAPoint_ThrowsTomlException(string toml, string expectedInMessage)
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Point3DHolder>(toml, Options));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_ABareInteger_ThrowsTomlException()
    {
        Assert.Throws<TomlException>(() => TomlUtils.Deserialize<Point3DHolder>("location = 5\n", Options));
    }

    [Fact]
    public void Serialize_WritesTheQuotedText()
    {
        var toml = TomlUtils.Serialize(new Point3DHolder { Location = new(1495, 1629, 10) }, Options);

        Assert.Equal("location = \"(1495, 1629, 10)\"", toml.Trim());
    }

    [Fact]
    public void Serialize_UnderACultureWithAnotherMinusSign_StillWritesAnAsciiMinus()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new("sv-SE");

            var toml = TomlUtils.Serialize(new Point3DHolder { Location = new(-5, 7, -20) }, Options);

            Assert.Equal("location = \"(-5, 7, -20)\"", toml.Trim());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void RoundTrip_PreservesTheValue()
    {
        var original = new Point3DHolder { Location = new(-12, 3400, 55) };

        var toml = TomlUtils.Serialize(original, Options);
        var restored = TomlUtils.Deserialize<Point3DHolder>(toml, Options);

        Assert.Equal(original.Location, restored!.Location);
    }
}
