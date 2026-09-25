using Moongate.Core.Primitives;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads a <see cref="HueSpec" /> from a bare TOML integer, such as <c>hue = 1150</c> or <c>hue = 0x047E</c>, or
///     from a quoted string in any format <see cref="HueSpec.Parse" /> accepts, such as <c>hue = "1150-1200"</c>.
///     Writes a fixed hue as a bare integer and a range as a quoted string.
/// </summary>
public sealed class HueSpecTomlConverter : TomlConverter<HueSpec>
{
    /// <inheritdoc />
    public override HueSpec Read(TomlReader reader)
    {
        switch (reader.TokenType)
        {
            case TomlTokenType.Integer:
                {
                    var value = reader.GetInt64();

                    if (value is < 0 or > 0xFFFF)
                    {
                        throw reader.CreateException($"{value} is not a hue; a hue must be between 0 and 0xFFFF.");
                    }

                    return HueSpec.FromValue((int)value);
                }
            case TomlTokenType.String:
                {
                    var text = reader.GetString();

                    if (!HueSpec.TryParse(text, out var spec))
                    {
                        throw reader.CreateException($"'{text}' is not a hue or a hue range.");
                    }

                    return spec;
                }
            default:
                throw reader.CreateException("Expected a hue number or a hue range string.");
        }
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, HueSpec value)
    {
        if (value.IsRange)
        {
            writer.WriteStringValue(value.ToString());

            return;
        }

        writer.WriteIntegerValue(value.Min);
    }
}
