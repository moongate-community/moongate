using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class HueSpecTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new HueSpecTomlConverter()]
    };

    [Theory, InlineData("hue = 1150\n"), InlineData("hue = 0x047E\n"), InlineData("hue = \"0x047E\"\n")]
    public void Deserialize_AFixedHue_ReadsTheSameHue(string toml)
    {
        var holder = TomlUtils.Deserialize<HueSpecHolder>(toml, Options);

        Assert.Equal(HueSpec.FromValue(1150), holder!.Hue);
    }

    [Theory, InlineData("hue = \"1150-1200\"\n"), InlineData("hue = \"hue(1150:1200)\"\n")]
    public void Deserialize_ARange_ReadsTheRange(string toml)
    {
        var holder = TomlUtils.Deserialize<HueSpecHolder>(toml, Options);

        Assert.Equal(HueSpec.FromRange(1150, 1200), holder!.Hue);
    }

    [Theory, InlineData("hue = \"red\"\n", "red"), InlineData("hue = 70000\n", "70000"), InlineData("hue = -1\n", "-1")]
    public void Deserialize_NotAHue_ThrowsTomlException(string toml, string expectedInMessage)
    {
        var exception = Assert.Throws<TomlException>(() => TomlUtils.Deserialize<HueSpecHolder>(toml, Options));

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_AFloat_ThrowsTomlException()
    {
        Assert.Throws<TomlException>(() => TomlUtils.Deserialize<HueSpecHolder>("hue = 1.5\n", Options));
    }

    [Fact]
    public void Serialize_AFixedHue_WritesABareInteger()
    {
        var toml = TomlUtils.Serialize(new HueSpecHolder { Hue = HueSpec.FromValue(1150) }, Options);

        Assert.Equal("hue = 1150", toml.Trim());
    }

    [Fact]
    public void Serialize_ARange_WritesAQuotedRange()
    {
        var toml = TomlUtils.Serialize(new HueSpecHolder { Hue = HueSpec.FromRange(1150, 1200) }, Options);

        Assert.Equal("hue = \"0x047E-0x04B0\"", toml.Trim());
    }

    [Theory, InlineData(1150, 1150), InlineData(0, 0xFFFF)]
    public void RoundTrip_PreservesTheValue(int min, int max)
    {
        var original = min == max ? HueSpec.FromValue(min) : HueSpec.FromRange(min, max);

        var toml = TomlUtils.Serialize(new HueSpecHolder { Hue = original }, Options);

        Assert.Equal(original, TomlUtils.Deserialize<HueSpecHolder>(toml, Options)!.Hue);
    }
}
