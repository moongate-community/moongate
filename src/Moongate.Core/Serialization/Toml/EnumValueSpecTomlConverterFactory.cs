using Moongate.Core.Primitives;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Builds an <see cref="EnumValueSpecTomlConverter{TEnum}" /> for whichever closed
///     <see cref="EnumValueSpec{TEnum}" /> Tomlyn asks for, so one registration through
///     <see cref="Moongate.Core.Utils.TomlUtils.AddTomlConverter" /> covers every enum a template uses it for.
/// </summary>
public sealed class EnumValueSpecTomlConverterFactory : TomlConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EnumValueSpec<>);
    }

    /// <inheritdoc />
    public override TomlConverter CreateConverter(Type typeToConvert, TomlSerializerOptions options)
    {
        var enumType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(EnumValueSpecTomlConverter<>).MakeGenericType(enumType);

        return (TomlConverter)Activator.CreateInstance(converterType)!;
    }
}
