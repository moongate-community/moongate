using System.Globalization;
using Moongate.Core.Geometry;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads and writes a <see cref="Rectangle2D" /> as the quoted text <see cref="Rectangle2D.ToString()" /> produces,
///     the corner then the size, such as <c>bounds = "(44, 65)+(142, 94)"</c>. Always uses the invariant culture, so a
///     file reads the same on every machine.
/// </summary>
public sealed class Rectangle2DTomlConverter : TomlConverter<Rectangle2D>
{
    /// <inheritdoc />
    public override Rectangle2D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x, y)+(width, height)\" string for a Rectangle2D.");
        }

        var text = reader.GetString();

        if (!Rectangle2D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Rectangle2D, expected \"(x, y)+(width, height)\".");
        }

        return parsed;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, Rectangle2D value)
    {
        writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
