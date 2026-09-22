using Moongate.Core.Primitives;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
/// Builds a <see cref="RangeValueSpecTomlConverter{T}" /> for whichever closed
/// <see cref="RangeValueSpec{T}" /> Tomlyn asks for, so one registration through
/// <see cref="Moongate.Core.Utils.TomlUtils.AddTomlConverter" /> covers every numeric type a template
/// uses it for.
/// </summary>
public sealed class RangeValueSpecTomlConverterFactory : TomlConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(RangeValueSpec<>);

    /// <inheritdoc />
    public override TomlConverter CreateConverter(Type typeToConvert, TomlSerializerOptions options)
    {
        var numberType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(RangeValueSpecTomlConverter<>).MakeGenericType(numberType);

        return (TomlConverter)Activator.CreateInstance(converterType)!;
    }
}
