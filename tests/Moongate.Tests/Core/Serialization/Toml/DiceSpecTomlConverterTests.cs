using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Tests.Support.Serialization.Data;
using Tomlyn;

namespace Moongate.Tests.Core.Serialization.Toml;

public sealed class DiceSpecTomlConverterTests
{
    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new DiceSpecTomlConverter()]
    };

    [Theory,
     InlineData("value = 7\n", 7, 7),
     InlineData("value = -2500\n", -2500, -2500),
     InlineData("value = \"-2500\"\n", -2500, -2500),
     InlineData("value = \"2d6+3\"\n", 5, 15)]
    public void Deserialize_AnIntegerOrAnExpression_ReadsItsBounds(string toml, int min, int max)
    {
        var spec = TomlUtils.Deserialize<DiceSpecHolder>(toml, Options)!.Value;

        Assert.Equal((min, max), (spec.Min, spec.Max));
    }

    [Fact]
    public void Deserialize_AMalformedExpression_NamesTheText()
    {
        var error = Assert.ThrowsAny<Exception>(() => TomlUtils.Deserialize<DiceSpecHolder>("value = \"2d\"\n", Options));

        Assert.Contains("'2d'", error.ToString());
    }

    [Fact]
    public void Serialize_AConstantAsAnIntegerAndAnExpressionAsText()
    {
        Assert.Contains("value = 7", TomlUtils.Serialize(new DiceSpecHolder { Value = DiceSpec.FromValue(7) }, Options));
        Assert.Contains("value = \"2d6+3\"", TomlUtils.Serialize(new DiceSpecHolder { Value = DiceSpec.Parse("2d6+3") }, Options));
    }
}
