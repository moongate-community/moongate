using System.Text.Json;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class SerialTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new SerialTomlConverter()]
    };

    [Fact]
    public void Deserialize_ABareHexLiteral_ParsesTheSameSerial()
    {
        var holder = TomlUtils.Deserialize<SerialHolder>("item_id = 0x0FEF\n", Options);

        Assert.Equal(new(0x0FEF), holder!.ItemId);
    }

    [Fact]
    public void Deserialize_AQuotedHexString_ParsesTheSameSerial()
    {
        var holder = TomlUtils.Deserialize<SerialHolder>("item_id = \"0x0FEF\"\n", Options);

        Assert.Equal(new(0x0FEF), holder!.ItemId);
    }

    [Fact]
    public void Deserialize_AQuotedDecimalString_ParsesTheSameSerial()
    {
        var holder = TomlUtils.Deserialize<SerialHolder>("item_id = \"4079\"\n", Options);

        Assert.Equal(new(4079), holder!.ItemId);
    }

    [Fact]
    public void Deserialize_AQuotedTextThatIsNotASerial_ThrowsTomlException()
    {
        var exception = Assert.Throws<TomlException>(
            () => TomlUtils.Deserialize<SerialHolder>("item_id = \"not-a-serial\"\n", Options)
        );

        Assert.Contains("not-a-serial", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WritesABareInteger()
    {
        var toml = TomlUtils.Serialize(new SerialHolder { ItemId = new(0x0FEF) }, Options);

        Assert.Equal("item_id = 4079", toml.Trim());
    }

    [Fact]
    public void RoundTrip_PreservesTheValue()
    {
        var original = new SerialHolder { ItemId = new(0x40000001) };

        var toml = TomlUtils.Serialize(original, Options);
        var restored = TomlUtils.Deserialize<SerialHolder>(toml, Options);

        Assert.Equal(original.ItemId, restored!.ItemId);
    }
}
