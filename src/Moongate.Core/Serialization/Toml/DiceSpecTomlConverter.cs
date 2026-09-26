using Moongate.Core.Primitives;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads a <see cref="DiceSpec" /> from a bare integer, such as <c>karma = -2500</c>, or from a string, such as
///     <c>strength = "1d25+95"</c>. Writes a constant as an integer and an expression as the text it was parsed from.
/// </summary>
public sealed class DiceSpecTomlConverter : TomlConverter<DiceSpec>
{
    /// <inheritdoc />
    public override DiceSpec Read(TomlReader reader)
    {
        switch (reader.TokenType)
        {
            case TomlTokenType.Integer:
                return DiceSpec.FromValue(checked((int)reader.GetInt64()));
            case TomlTokenType.String:
                {
                    var text = reader.GetString();

                    if (!DiceSpec.TryParse(text, out var spec))
                    {
                        throw reader.CreateException($"'{text}' is not a number or a dice expression.");
                    }

                    return spec;
                }
            default:
                throw reader.CreateException("Expected a number or a dice expression.");
        }
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, DiceSpec value)
    {
        if (value.IsConstant)
        {
            writer.WriteIntegerValue(value.Min);

            return;
        }

        writer.WriteStringValue(value.ToString());
    }
}
