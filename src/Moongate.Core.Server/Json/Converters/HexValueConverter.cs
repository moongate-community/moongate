using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moongate.Core.Server.Json.Converters;

/// <summary>
/// Converts hex strings like "0x001" to integers
/// </summary>
public class HexValueConverter<T> : JsonConverter<T> where T : struct
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();

            /// Handle hex strings (0x prefix or just hex digits)
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
        writer.WriteNumberValue(Convert.ToInt32(value));
    }

    private static bool IsHexString(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.All(c => char.IsDigit(c) || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f');
    }
}
