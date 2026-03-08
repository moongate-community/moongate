using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Server.Modules;

[ScriptModule("map", "Provides map conversion helpers for scripts.")]
public sealed class MapModule
{
    private static readonly Dictionary<string, int> MapNameToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["felucca"] = 0,
        ["trammel"] = 1,
        ["ilshenar"] = 2,
        ["malas"] = 3,
        ["tokuno"] = 4,
        ["termur"] = 5,
        ["ter_mur"] = 5,
        ["internal"] = 0x7F
    };

    [ScriptFunction("to_id", "Converts a map name or id-like input to map id.")]
    public int ToId(object? value, int fallback = -1)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is double number)
        {
            return (int)number;
        }

        if (value is int mapId)
        {
            return mapId;
        }

        if (value is long longValue)
        {
            return (int)longValue;
        }

        if (value is string text)
        {
            var normalized = text.Trim();

            if (MapNameToId.TryGetValue(normalized, out var resolvedByName))
            {
                return resolvedByName;
            }

            if (int.TryParse(normalized, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }
}
