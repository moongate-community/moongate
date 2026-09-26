using System.Globalization;
using Moongate.Core.Geometry;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads and writes a <see cref="Point2D" /> as the quoted text <see cref="Point2D.ToString()" /> produces, such
///     as <c>position = "(1495, 1629)"</c>. Always uses the invariant culture, so a file reads the same on every
///     machine.
/// </summary>
public sealed class Point2DTomlConverter : TomlConverter<Point2D>
{
    /// <inheritdoc />
    public override Point2D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x, y)\" string for a Point2D.");
        }

        var text = reader.GetString();

        if (!Point2D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Point2D, expected \"(x, y)\".");
        }

        return parsed;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, Point2D value)
    {
        writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
