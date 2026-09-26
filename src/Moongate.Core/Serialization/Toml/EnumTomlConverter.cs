using Moongate.Core.Utils;
using Tomlyn;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads and writes a <typeparamref name="TEnum" /> as its snake_case name, or names joined by <c>|</c> for flags,
///     through <see cref="EnumNameUtils" />. Numbers are never written and never accepted.
/// </summary>
public sealed class EnumTomlConverter<TEnum> : TomlConverter<TEnum> where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override TEnum Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException($"Expected a {typeof(TEnum).Name} name as a string, such as \"{Example()}\".");
        }

        var text = reader.GetString();

        if (!EnumNameUtils.TryParse<TEnum>(text, out var value))
        {
            throw reader.CreateException(
                $"'{text}' is not a {typeof(TEnum).Name}; use one of {string.Join(", ", Enum.GetNames<TEnum>().Select(StringUtils.ToSnakeCase))}."
            );
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, TEnum value)
    {
        try
        {
            writer.WriteStringValue(EnumNameUtils.Format(value));
        }
        catch (ArgumentException exception)
        {
            throw new TomlException(exception.Message);
        }
    }

    private static string Example()
    {
        var names = Enum.GetNames<TEnum>();

        return names.Length > 0 ? StringUtils.ToSnakeCase(names[0]) : string.Empty;
    }
}
