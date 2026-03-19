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

        var entries = new List<LuaBrainContextMenuEntry>(8);

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

        if (value.Type != DataType.Table || value.Table is null)
        {
            return false;
        }

        var keyValue = value.Table.Get("key");
        var clilocValue = value.Table.Get("cliloc_id");

        var key = keyValue.Type == DataType.String ? keyValue.String : null;
        var clilocId = TryReadClilocId(clilocValue);

        if (string.IsNullOrWhiteSpace(key))
        {
            var tupleKey = value.Table.Get(1);

            if (tupleKey.Type == DataType.String)
            {
                key = tupleKey.String;
            }
        }

        if (clilocId is null)
        {
            clilocId = TryReadClilocId(value.Table.Get(2));
        }

        if (string.IsNullOrWhiteSpace(key) || clilocId is null || clilocId.Value < 3_000_000)
        {
            return false;
        }

        entry = new(key.Trim(), clilocId.Value);

        return true;
    }

    private static int? TryReadClilocId(DynValue value)
    {
        if (value.Type == DataType.Number)
        {
            var numeric = (int)Math.Truncate(value.Number);

            return numeric;
        }

        if (value.Type == DataType.String &&
            int.TryParse(value.String, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
