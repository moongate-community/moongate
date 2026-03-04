using System.Text.Json;
using System.Text.Json.Serialization;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.UO.Data.Json.Converters;

/// <summary>
/// Converts <see cref="HueSpec" /> values from numeric, hex, or <c>hue(min:max)</c> string formats.
/// </summary>
public sealed class HueSpecJsonConverter : JsonConverter<HueSpec>
{
    public override HueSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => HueSpec.FromValue(reader.GetInt32()),
            JsonTokenType.String => Parse(reader.GetString()),
            _                    => throw new JsonException($"Unsupported token type for HueSpec: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, HueSpec value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    private static HueSpec Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("HueSpec cannot be null or empty.");
        }

        try
        {
            return HueSpec.ParseFromString(value);
        }
        catch (FormatException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }
}
