using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zanaptak.PcgRandom;

namespace Moongate.Core.Server.Json.Converters;

/// <summary>
/// JSON converter that handles hue expressions like "hue(min:max)" and hex values like "0x001"
/// </summary>
public class HueValueConverter<T> : JsonConverter<T> where T : struct
{
    private static readonly Pcg _random = new();

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();

            /// Handle hue expressions like "hue(1:50)"
            if (str.StartsWith("hue(") && str.EndsWith(")"))
            {
                return (T)ParseHueExpression(str, typeof(T));
            }

            /// Handle hex strings (0x prefix)
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = Convert.ToInt32(str, 16);

                return (T)Convert.ChangeType(hexValue, typeof(T));
            }

            /// Handle plain hex strings without 0x prefix
            if (IsHexString(str))
            {
                var hexValue = Convert.ToInt32(str, 16);

                return (T)Convert.ChangeType(hexValue, typeof(T));
            }
        }

        /// Normal numeric parsing
        return typeof(T) switch
        {
            var t when t == typeof(int)    => (T)(object)reader.GetInt32(),
            var t when t == typeof(byte)   => (T)(object)reader.GetByte(),
            var t when t == typeof(ushort) => (T)(object)reader.GetUInt16(),
            var t when t == typeof(uint)   => (T)(object)reader.GetUInt32(),
            _                              => throw new JsonException($"Unsupported type: {typeof(T)}")
        };
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        /// Write as hex string with 0x prefix for better readability
        var intValue = Convert.ToInt32(value);
        writer.WriteStringValue($"0x{intValue:X4}");
    }

    /// <summary>
    /// Checks if a string contains only hexadecimal characters
    /// </summary>
    private static bool IsHexString(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.All(c => char.IsDigit(c) || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f');
    }

    /// <summary>
    /// Parses hue expressions like "hue(1:50)" and returns a random value in the range
    /// </summary>
    private static object ParseHueExpression(string expression, Type targetType)
    {
        var match = Regex.Match(expression, @"hue\((\d+):(\d+)\)");

        if (!match.Success)
        {
            throw new JsonException($"Invalid hue expression: {expression}. Expected format: hue(min:max)");
        }

        var minStr = match.Groups[1].Value;
        var maxStr = match.Groups[2].Value;

        if (!int.TryParse(minStr, out var min) || !int.TryParse(maxStr, out var max))
        {
            throw new JsonException($"Invalid hue range values in: {expression}");
        }

        if (min > max)
        {
            throw new JsonException($"Invalid hue range: min ({min}) cannot be greater than max ({max})");
        }

        /// Generate random hue value in the specified range (inclusive)
        var randomHue = _random.Next(min, max + 1);

        return Convert.ChangeType(randomHue, targetType);
    }
}
