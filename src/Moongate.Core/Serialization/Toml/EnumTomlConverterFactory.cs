using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Builds an <see cref="EnumTomlConverter{TEnum}" /> for every enum Tomlyn meets. <c>TomlUtils</c> always includes
///     it in its default options, so every enum in a Moongate TOML file is written and read by name.
/// </summary>
public sealed class EnumTomlConverterFactory : TomlConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    /// <inheritdoc />
    public override TomlConverter CreateConverter(Type typeToConvert, TomlSerializerOptions options)
    {
        return (TomlConverter)Activator.CreateInstance(typeof(EnumTomlConverter<>).MakeGenericType(typeToConvert))!;
    }
}
