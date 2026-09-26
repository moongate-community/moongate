using System.Globalization;
using Moongate.Core.Geometry;
using Tomlyn.Serialization;

namespace Moongate.Core.Serialization.Toml;

/// <summary>
///     Reads and writes a <see cref="Point3D" /> as the quoted text <see cref="Point3D.ToString()" /> produces, such
///     as <c>location = "(1495, 1629, 10)"</c>. Always uses the invariant culture, so a file reads the same on every
///     machine.
/// </summary>
public sealed class Point3DTomlConverter : TomlConverter<Point3D>
{
    /// <inheritdoc />
    public override Point3D Read(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.String)
        {
            throw reader.CreateException("Expected a \"(x, y, z)\" string for a Point3D.");
        }

        var text = reader.GetString();

        if (!Point3D.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            throw reader.CreateException($"'{text}' is not a valid Point3D, expected \"(x, y, z)\".");
        }

        return parsed;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, Point3D value)
    {
        writer.WriteStringValue(value.ToString(null, CultureInfo.InvariantCulture));
    }
}
