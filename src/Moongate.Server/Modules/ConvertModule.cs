using System.Globalization;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.UO.Data.Geometry;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("convert", "Provides conversion helpers for scripts.")]
public sealed class ConvertModule
{
    [ScriptFunction("to_bool", "Converts a value to boolean.")]
    public bool ToBool(object? value, bool fallback = false)
    {
        if (value is null)
        {
            return fallback;
        }

        switch (value)
        {
            case bool boolValue:
                return boolValue;
            case int intValue:
                return intValue != 0;
            case long longValue:
                return longValue != 0;
            case double doubleValue:
                return Math.Abs(doubleValue) > double.Epsilon;
            case string text:
            {
                var normalized = text.Trim().ToLowerInvariant();

                if (normalized is "true" or "1" or "yes" or "on")
                {
                    return true;
                }

                if (normalized is "false" or "0" or "no" or "off")
                {
                    return false;
                }

                return fallback;
            }
            default:
                return fallback;
        }
    }

    [ScriptFunction("to_int", "Converts a value to integer, supporting decimal and 0x hex strings.")]
    public long ToInt(object? value, long fallback = 0)
    {
        if (value is null)
        {
            return fallback;
        }

        switch (value)
        {
            case int intValue:
                return intValue;
            case long longValue:
                return longValue;
            case double doubleValue:
                return (long)doubleValue;
            case string text:
            {
                var normalized = text.Trim();

                if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    long.TryParse(normalized.AsSpan()[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                {
                    return hex;
                }

                if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                return fallback;
            }
            default:
                return fallback;
        }
    }

    [ScriptFunction("parse_delay_ms", "Parses delay to milliseconds. Supports integer ms or hh:mm:ss.")]
    public long ParseDelayMilliseconds(object? value, long fallback = 0)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is int intValue)
        {
            return Math.Max(0, intValue);
        }

        if (value is long longValue)
        {
            return Math.Max(0, longValue);
        }

        if (value is double doubleValue)
        {
            return Math.Max(0, (long)doubleValue);
        }

        if (value is not string text)
        {
            return fallback;
        }

        var normalized = text.Trim();

        if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMs))
        {
            return Math.Max(0, parsedMs);
        }

        if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var parsedDelay))
        {
            return Math.Max(0, (long)parsedDelay.TotalMilliseconds);
        }

        return fallback;
    }

    [ScriptFunction("parse_point3d", "Parses '(x, y, z)' into a Lua table { x, y, z }.")]
    public Table? ParsePoint3D(object? value)
    {
        if (value is not string text || !Point3D.TryParse(text, CultureInfo.InvariantCulture, out var point))
        {
            return null;
        }

        var table = new Table(new Script());
        table["x"] = point.X;
        table["y"] = point.Y;
        table["z"] = point.Z;

        return table;
    }
}
