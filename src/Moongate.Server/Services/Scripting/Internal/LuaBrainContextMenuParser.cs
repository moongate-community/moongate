using Moongate.Server.Data.Internal.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Parses custom context menu entries returned by Lua brain scripts.
/// </summary>
internal static class LuaBrainContextMenuParser
{
    private const int MaxContextMenuEntries = 32;

    public static IReadOnlyList<LuaBrainContextMenuEntry> Parse(DynValue result)
    {
        if (result.Type != DataType.Table || result.Table is null)
        {
            return [];
        }

        var entries = new List<LuaBrainContextMenuEntry>(capacity: 8);

        for (var index = 1; index <= MaxContextMenuEntries; index++)
        {
            var value = result.Table.Get(index);

            if (value.IsNil())
            {
                break;
            }

            if (!TryParseEntry(value, out var entry))
            {
                continue;
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static bool TryParseEntry(DynValue value, out LuaBrainContextMenuEntry entry)
    {
        entry = null!;

        if (value.Type == DataType.String && !string.IsNullOrWhiteSpace(value.String))
        {
            var keyText = value.String.Trim();
            entry = new(keyText, keyText);

            return true;
        }

        if (value.Type != DataType.Table || value.Table is null)
        {
            return false;
        }

        var keyValue = value.Table.Get("key");
        var textValue = value.Table.Get("text");

        var key = keyValue.Type == DataType.String ? keyValue.String : null;
        var text = textValue.Type == DataType.String ? textValue.String : null;

        if (string.IsNullOrWhiteSpace(key))
        {
            var tupleKey = value.Table.Get(1);

            if (tupleKey.Type == DataType.String)
            {
                key = tupleKey.String;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            var tupleText = value.Table.Get(2);

            if (tupleText.Type == DataType.String)
            {
                text = tupleText.String;
            }
        }

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        entry = new(key.Trim(), text.Trim());

        return true;
    }
}
