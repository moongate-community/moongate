using Moongate.Core.Primitives;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads a <see cref="Serial" /> from a bare TOML integer, such as
///     <c>
///         item_id = 0x0FEF
///     </c>
///     , or from a
///     quoted string in the same notation, such as
///     <c>
///         item_id = "0x0FEF"
///     </c>
///     . Always writes a bare integer.
/// </summary>
public sealed class SerialTomlConverter : TomlConverter<Serial>
{
    /// <inheritdoc />
    public override Serial Read(TomlReader reader)
    {
        if (reader.TokenType == TomlTokenType.String)
        {
            var text = reader.GetString();

            if (!Serial.TryParse(text, out var parsed))
            {
                throw reader.CreateException($"'{text}' is not a valid serial.");
            }

            return parsed;
        }

        return new((uint)reader.GetInt64());
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, Serial value)
    {
        writer.WriteIntegerValue(value.Value);
    }
}
