using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class RangeValueSpecTomlConverterFactoryTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new RangeValueSpecTomlConverterFactory()]
    };

    [Fact]
    public void Deserialize_ABareInteger_ResolvesToThatFixedValue()
    {
        var holder = TomlUtils.Deserialize<HueHolder>("hue = 1150\n", Options);

        Assert.False(holder!.Hue.IsRandom);
        Assert.Equal(1150, holder.Hue.Resolve());
    }

    [Fact]
    public void Deserialize_AQuotedFixedNumber_ResolvesToThatFixedValue()
    {
        var holder = TomlUtils.Deserialize<HueHolder>("hue = \"1150\"\n", Options);

        Assert.False(holder!.Hue.IsRandom);
        Assert.Equal(1150, holder.Hue.Resolve());
    }

    [Fact]
    public void Deserialize_AQuotedRange_ResolvesToAValueInTheRange()
    {
        var holder = TomlUtils.Deserialize<HueHolder>("hue = \"1150-1200\"\n", Options);

        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(holder!.Hue.Resolve(), 1150, 1200);
        }
    }

    [Fact]
    public void Deserialize_AnInvalidSpec_ThrowsTomlExceptionNamingTheText()
    {
        var exception =
            Assert.Throws<TomlException>(() => TomlUtils.Deserialize<HueHolder>("hue = \"not-a-range\"\n", Options)
            );

        Assert.Contains("not-a-range", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_AFixedValue_WritesABareInteger()
    {
        var toml = TomlUtils.Serialize(new HueHolder { Hue = RangeValueSpec<int>.FromValue(1150) }, Options);

        Assert.Equal("hue = 1150", toml.Trim());
    }

    [Fact]
    public void Serialize_ARange_WritesTheQuotedMinDashMaxForm()
    {
        var toml = TomlUtils.Serialize(
            new HueHolder { Hue = RangeValueSpec<int>.FromRange(1150, 1200) },
            Options
        );

        Assert.Equal("hue = \"1150-1200\"", toml.Trim());
    }

    [Fact]
    public void RoundTrip_AFixedValue_PreservesIt()
    {
        var original = new HueHolder { Hue = RangeValueSpec<int>.FromValue(2101) };

        var toml = TomlUtils.Serialize(original, Options);
        var restored = TomlUtils.Deserialize<HueHolder>(toml, Options);

        Assert.Equal(original.Hue.Resolve(), restored!.Hue.Resolve());
    }
}
