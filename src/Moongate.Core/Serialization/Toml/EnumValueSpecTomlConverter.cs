using Moongate.Core.Primitives;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads and writes an <see cref="EnumValueSpec{TEnum}" /> as the text it documents.
/// </summary>
public sealed class EnumValueSpecTomlConverter<TEnum> : TomlConverter<EnumValueSpec<TEnum>> where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override EnumValueSpec<TEnum> Read(TomlReader reader)
    {
        var text = reader.GetString();

        if (!EnumValueSpec<TEnum>.TryParse(text, out var spec))
        {
            throw reader.CreateException($"'{text}' is not a valid {typeof(TEnum).Name} value or random_of spec.");
        }

        return spec;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, EnumValueSpec<TEnum> value)
    {
        writer.WriteStringValue(value.ToString());
    }
}
