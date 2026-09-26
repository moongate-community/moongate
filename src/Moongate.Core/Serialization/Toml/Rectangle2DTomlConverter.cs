using System.Globalization;
using Moongate.Core.Geometry;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Writes a <see cref="Rectangle2D" /> as two corners, such as <c>bounds = "(44, 65)..(186, 159)"</c>.
///     The first corner is included and the second is excluded. Also reads the legacy corner-plus-size format.
///     Always uses the invariant culture, so a file reads the same on every machine.
/// </summary>
public sealed class Rectangle2DTomlConverter : TomlConverter<Rectangle2D>
{
    /// <inheritdoc />
    public override Rectangle2D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x1, y1)..(x2, y2)\" string for a Rectangle2D.");
        }

        var text = reader.GetString();

        var separator = text.IndexOf("..", StringComparison.Ordinal);

        if (separator >= 0 &&
            Point2D.TryParse(text.AsSpan(0, separator), CultureInfo.InvariantCulture, out var start) &&
            Point2D.TryParse(text.AsSpan(separator + 2), CultureInfo.InvariantCulture, out var end))
        {
            return new(start, end);
        }

        if (separator >= 0 || !Rectangle2D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Rectangle2D, expected \"(x1, y1)..(x2, y2)\".");
        }

        return parsed;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, Rectangle2D value)
    {
        writer.WriteStringValue(
            $"{value.Start.ToString(null, CultureInfo.InvariantCulture)}..{value.End.ToString(null, CultureInfo.InvariantCulture)}"
        );
    }
}
