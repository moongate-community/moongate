using Moongate.Core.Geometry;
using Moongate.Core.Serialization.Toml;
using Moongate.Server.Ultima.Data.Regions;
using Tomlyn.Serialization;

namespace Moongate.Server.Ultima.Serialization.Toml;

/// <summary>
///     Reads and writes region areas as two quoted corners, or an inline table with bounds and optional height limits.
///     Also reads legacy x1/y1/x2/y2 tables. The start is included and the end is excluded on every axis.
/// </summary>
public sealed class RegionAreaContentTomlConverter : TomlConverter<RegionAreaContent>
{
    private readonly Rectangle2DTomlConverter _boundsConverter = new();

    /// <inheritdoc />
    public override RegionAreaContent Read(TomlReader reader)
    {
        if (reader.TokenType == TomlTokenType.String)
        {
            var bounds = _boundsConverter.Read(reader);

            return new() { X1 = bounds.Start.X, Y1 = bounds.Start.Y, X2 = bounds.End.X, Y2 = bounds.End.Y };
        }

        if (reader.TokenType != TomlTokenType.StartTable)
        {
            throw reader.CreateException("Expected a region area string or a table with bounds and optional z1/z2.");
        }

        var area = new RegionAreaContent();
        Rectangle2D? rectangle = null;
        var coordinates = 0;
        reader.Read();

        while (reader.TokenType == TomlTokenType.PropertyName)
        {
            var property = reader.PropertyName;
            reader.Read();

            switch (property)
            {
                case "bounds":
                    rectangle = _boundsConverter.Read(reader);
                    break;
                case "x1":
                    area.X1 = ReadCoordinate(reader);
                    coordinates |= 1;
                    break;
                case "y1":
                    area.Y1 = ReadCoordinate(reader);
                    coordinates |= 2;
                    break;
                case "x2":
                    area.X2 = ReadCoordinate(reader);
                    coordinates |= 4;
                    break;
                case "y2":
                    area.Y2 = ReadCoordinate(reader);
                    coordinates |= 8;
                    break;
                case "z1":
                    area.Z1 = ReadCoordinate(reader);
                    break;
                case "z2":
                    area.Z2 = ReadCoordinate(reader);
                    break;
                default:
                    throw reader.CreateException($"Unknown region area field '{property}'.");
            }

            reader.Read();
        }

        if (reader.TokenType != TomlTokenType.EndTable)
        {
            throw reader.CreateException("Expected the end of a region area table.");
        }

        if (rectangle is { } parsed)
        {
            if (coordinates != 0)
            {
                throw reader.CreateException("Use bounds or x1/y1/x2/y2 for a region area, not both.");
            }

            area.X1 = parsed.Start.X;
            area.Y1 = parsed.Start.Y;
            area.X2 = parsed.End.X;
            area.Y2 = parsed.End.Y;
        }
        else if (coordinates != 15)
        {
            throw reader.CreateException("A region area table requires bounds or all of x1/y1/x2/y2.");
        }

        reader.Read();

        return area;
    }

    /// <inheritdoc />
    public override void Write(TomlWriter writer, RegionAreaContent value)
    {
        var bounds = new Rectangle2D(new Point2D(value.X1, value.Y1), new Point2D(value.X2, value.Y2));

        if (value.Z1 is null && value.Z2 is null)
        {
            _boundsConverter.Write(writer, bounds);

            return;
        }

        writer.WriteStartInlineTable();
        writer.WritePropertyName("bounds");
        _boundsConverter.Write(writer, bounds);

        if (value.Z1 is { } low)
        {
            writer.WritePropertyName("z1");
            writer.WriteIntegerValue(low);
        }

        if (value.Z2 is { } high)
        {
            writer.WritePropertyName("z2");
            writer.WriteIntegerValue(high);
        }

        writer.WriteEndInlineTable();
    }

    private static int ReadCoordinate(TomlReader reader)
    {
        if (reader.TokenType != TomlTokenType.Integer)
        {
            throw reader.CreateException("Region area coordinates must be integers.");
        }

        var value = reader.GetInt64();

        if (value < int.MinValue || value > int.MaxValue)
        {
            throw reader.CreateException("Region area coordinates must fit in a 32-bit integer.");
        }

        return (int)value;
    }
}
