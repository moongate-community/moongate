using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Moongate.Core.Random.DiceNotation;
using Zanaptak.PcgRandom;

namespace Moongate.Core.Server.Json.Converters;

/// <summary>
/// JSON converter that handles random value expressions
/// </summary>
public class RandomValueConverter<T> : JsonConverter<T>
{
    private static readonly Pcg _random = new();

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();

            /// Handle random expressions
            if (str.StartsWith("random(") && str.EndsWith(")"))
            {
                return (T)ParseRandomExpression(str, typeof(T));
            }

            /// Handle dice expressions
            if (str.StartsWith("dice(") && str.EndsWith(")"))
            {
                var diceExpr = str.Substring(5, str.Length - 6); // Remove "dice(" and ")"
                var result = new DiceParser().Parse(diceExpr).Roll().Value;

                return (T)Convert.ChangeType(result, typeof(T));
            }

            /// Handle numeric strings like "0", "123", etc.
            if (TryParseNumericString(str, typeof(T), out var numericValue))
            {
                return (T)numericValue;
            }

            /// Handle regular string values for string type
            if (typeof(T) == typeof(string))
            {
                return (T)(object)str;
            }

            throw new JsonException($"Cannot convert string '{str}' to type {typeof(T)}");
        }

        /// Normal value parsing for non-string tokens
        return typeof(T) switch
        {
            var t when t == typeof(int)    => (T)(object)reader.GetInt32(),
            var t when t == typeof(double) => (T)(object)reader.GetDouble(),
            var t when t == typeof(string) => (T)(object)reader.GetString(),
            _                              => throw new JsonException($"Unsupported type: {typeof(T)}")
        };
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }

    private static object ParseRandomExpression(string expression, Type targetType)
    {
        var match = Regex.Match(expression, @"random\((.+)\)");

        if (!match.Success)
        {
            throw new JsonException($"Invalid random expression: {expression}");
        }

        var content = match.Groups[1].Value.Trim();
        var parts = content.Split(',').Select(p => p.Trim()).ToArray();

        if (parts.Length == 2 && targetType == typeof(int))
        {
            var min = int.Parse(parts[0]);
            var max = int.Parse(parts[1]);

            return _random.Next(min, max + 1);
        }

        if (parts.Length == 2 && targetType == typeof(double))
        {
            var min = double.Parse(parts[0]);
            var max = double.Parse(parts[1]);

            return min + _random.NextDouble() * (max - min);
        }

        if (targetType == typeof(string))
        {
            return parts[_random.Next(parts.Length)];
        }

        throw new JsonException($"Unsupported random expression: {expression}");
    }

    /// <summary>
    /// Tries to parse a string as a numeric value
    /// </summary>
    private static bool TryParseNumericString(string str, Type targetType, out object result)
    {
        result = null;

        if (string.IsNullOrEmpty(str))
        {
            return false;
        }

        try
        {
            if (targetType == typeof(int))
            {
                if (int.TryParse(str, out var intValue))
                {
                    result = intValue;

                    return true;
                }
            }
            else if (targetType == typeof(double))
            {
                if (double.TryParse(str, out var doubleValue))
                {
                    result = doubleValue;

                    return true;
                }
            }
            else if (targetType == typeof(float))
            {
                if (float.TryParse(str, out var floatValue))
                {
                    result = floatValue;

                    return true;
                }
            }
            else if (targetType == typeof(decimal))
            {
                if (decimal.TryParse(str, out var decimalValue))
                {
                    result = decimalValue;

                    return true;
                }
            }
            else if (targetType == typeof(long))
            {
                if (long.TryParse(str, out var longValue))
                {
                    result = longValue;

                    return true;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }
}
