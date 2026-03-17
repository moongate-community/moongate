using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Internal.Scripting;

internal sealed class AsyncLuaValueConverter
{
    public bool TryConvertPayload(Table? payload, out Dictionary<string, object?> converted)
    {
        converted = new(StringComparer.Ordinal);

        if (payload is null)
        {
            return true;
        }

        foreach (var pair in payload.Pairs)
        {
            if (pair.Key.Type != DataType.String || string.IsNullOrWhiteSpace(pair.Key.String))
            {
                return false;
            }

            if (!TryConvertValue(pair.Value, out var value))
            {
                return false;
            }

            converted[pair.Key.String] = value;
        }

        return true;
    }

    public Table ToLuaTable(Script script, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(values);

        var table = new Table(script);

        foreach (var pair in values)
        {
            table[pair.Key] = ToDynValue(script, pair.Value);
        }

        return table;
    }

    private bool TryConvertValue(DynValue value, out object? converted)
    {
        switch (value.Type)
        {
            case DataType.Nil:
            case DataType.Void:
                converted = null;
                return true;
            case DataType.Boolean:
                converted = value.Boolean;
                return true;
            case DataType.Number:
                converted = value.Number;
                return true;
            case DataType.String:
                converted = value.String;
                return true;
            case DataType.Table:
                return TryConvertTable(value.Table!, out converted);
            default:
                converted = null;
                return false;
        }
    }

    private bool TryConvertTable(Table table, out object? converted)
    {
        if (IsSequentialArray(table))
        {
            var list = new List<object?>(table.Length);

            for (var index = 1; index <= table.Length; index++)
            {
                if (!TryConvertValue(table.Get(index), out var item))
                {
                    converted = null;
                    return false;
                }

                list.Add(item);
            }

            converted = list;
            return true;
        }

        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var pair in table.Pairs)
        {
            if (pair.Key.Type != DataType.String || string.IsNullOrWhiteSpace(pair.Key.String))
            {
                converted = null;
                return false;
            }

            if (!TryConvertValue(pair.Value, out var value))
            {
                converted = null;
                return false;
            }

            dictionary[pair.Key.String] = value;
        }

        converted = dictionary;
        return true;
    }

    private DynValue ToDynValue(Script script, object? value)
        => value switch
        {
            null => DynValue.Nil,
            bool boolean => DynValue.NewBoolean(boolean),
            string text => DynValue.NewString(text),
            sbyte signed8 => DynValue.NewNumber(signed8),
            byte unsigned8 => DynValue.NewNumber(unsigned8),
            short signed16 => DynValue.NewNumber(signed16),
            ushort unsigned16 => DynValue.NewNumber(unsigned16),
            int signed32 => DynValue.NewNumber(signed32),
            uint unsigned32 => DynValue.NewNumber(unsigned32),
            long signed64 => DynValue.NewNumber(signed64),
            ulong unsigned64 => DynValue.NewNumber(unsigned64),
            float single => DynValue.NewNumber(single),
            double number => DynValue.NewNumber(number),
            decimal money => DynValue.NewNumber((double)money),
            Dictionary<string, object?> dictionary => ToDictionaryTable(script, dictionary),
            List<object?> list => ToListTable(script, list),
            IReadOnlyDictionary<string, object?> dictionary => ToDictionaryTable(script, dictionary),
            IReadOnlyList<object?> list => ToListTable(script, list),
            _ => DynValue.NewString(value.ToString() ?? string.Empty)
        };

    private DynValue ToDictionaryTable(Script script, IReadOnlyDictionary<string, object?> values)
    {
        var table = new Table(script);

        foreach (var pair in values)
        {
            table[pair.Key] = ToDynValue(script, pair.Value);
        }

        return DynValue.NewTable(table);
    }

    private DynValue ToListTable(Script script, IReadOnlyList<object?> values)
    {
        var table = new Table(script);

        for (var index = 0; index < values.Count; index++)
        {
            table[index + 1] = ToDynValue(script, values[index]);
        }

        return DynValue.NewTable(table);
    }

    private static bool IsSequentialArray(Table table)
    {
        if (table.Length <= 0)
        {
            return false;
        }

        foreach (var pair in table.Pairs)
        {
            if (pair.Key.Type != DataType.Number)
            {
                return false;
            }

            var numericKey = pair.Key.Number;
            var integerKey = Math.Truncate(numericKey);

            if (Math.Abs(numericKey - integerKey) > double.Epsilon || integerKey < 1 || integerKey > table.Length)
            {
                return false;
            }
        }

        return true;
    }
}
