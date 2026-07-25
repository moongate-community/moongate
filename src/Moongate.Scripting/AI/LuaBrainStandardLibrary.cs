using MoonSharp.Interpreter;

namespace Moongate.Scripting.AI;

internal sealed class LuaBrainStandardLibrary
{
    private const int MaximumCallbackArguments = 1024;
    private const int MaximumStringRepetitions = 4096;
    private const int MaximumStringCharacters = 4096;
    private const int MaximumTableElements = 1024;
    private static readonly string[] GlobalCallbackNames =
    [
        "ipairs",
        "next",
        "pairs",
        "tostring",
        "type"
    ];
    private static readonly string[] MathCallbackNames =
    [
        "abs",
        "acos",
        "asin",
        "atan",
        "atan2",
        "ceil",
        "cos",
        "cosh",
        "deg",
        "exp",
        "floor",
        "fmod",
        "frexp",
        "ldexp",
        "log",
        "max",
        "min",
        "modf",
        "pow",
        "rad",
        "random",
        "randomseed",
        "sin",
        "sinh",
        "sqrt",
        "tan",
        "tanh"
    ];
    private static readonly string[] MathConstantNames = ["huge", "pi"];
    private readonly Script _script;

    public LuaBrainStandardLibrary(Script script)
    {
        _script = script;
    }

    public Table CreateEnvironment(Table brainApi, out Table stringMetatable)
    {
        var environment = new Table(_script);
        environment.Set("brain", DynValue.NewTable(brainApi));

        foreach (var name in GlobalCallbackNames)
        {
            CopyBoundedCallback(
                _script.Globals,
                environment,
                name,
                arguments => EnsureArgumentCount(name, arguments)
            );
        }

        CopyBoundedCallback(
            _script.Globals,
            environment,
            "assert",
            arguments =>
            {
                EnsureArgumentCount("assert", arguments);
                EnsureStringValueLength("assert", arguments, 1);
            }
        );
        CopyBoundedCallback(
            _script.Globals,
            environment,
            "error",
            arguments =>
            {
                EnsureArgumentCount("error", arguments);
                EnsureStringValueLength("error", arguments, 0);
            }
        );
        CopyBoundedCallback(
            _script.Globals,
            environment,
            "pcall",
            arguments => EnsureArgumentCount("pcall", arguments)
        );
        CopyBoundedCallback(
            _script.Globals,
            environment,
            "select",
            arguments => EnsureArgumentCount("select", arguments)
        );
        CopyBoundedCallback(
            _script.Globals,
            environment,
            "tonumber",
            arguments =>
            {
                EnsureArgumentCount("tonumber", arguments);
                EnsureStringValueLength("tonumber", arguments, 0);
            }
        );
        CopyBoundedCallback(
            _script.Globals,
            environment,
            "xpcall",
            arguments => EnsureArgumentCount("xpcall", arguments)
        );

        var math = CreateMathLibrary();
        var strings = CreateStringLibrary();
        var tables = CreateTableLibrary();
        environment.Set("math", DynValue.NewTable(math));
        environment.Set("string", DynValue.NewTable(strings));
        environment.Set("table", DynValue.NewTable(tables));
        stringMetatable = new(_script);
        stringMetatable.Set("__index", DynValue.NewTable(strings));

        return environment;
    }

    public T RunWithStringMetatable<T>(Table stringMetatable, Func<T> action)
    {
        var previous = _script.GetTypeMetatable(DataType.String);
        _script.SetTypeMetatable(DataType.String, stringMetatable);

        try
        {
            return action();
        }
        finally
        {
            _script.SetTypeMetatable(DataType.String, previous);
        }
    }

    private Table CreateMathLibrary()
    {
        var source = GetLibrary("math");
        var copy = new Table(_script);

        foreach (var name in MathCallbackNames)
        {
            CopyBoundedCallback(
                source,
                copy,
                name,
                arguments => EnsureArgumentCount($"math.{name}", arguments)
            );
        }

        foreach (var name in MathConstantNames)
        {
            CopyValue(source, copy, name);
        }

        return copy;
    }

    private Table CreateStringLibrary()
    {
        var source = GetLibrary("string");
        var copy = new Table(_script);
        CopyBoundedCallback(
            source,
            copy,
            "byte",
            arguments => EnsureStringLength("string.byte", arguments, 0, false)
        );
        CopyBoundedCallback(
            source,
            copy,
            "char",
            arguments => EnsureArgumentCount("string.char", arguments)
        );
        CopyBoundedCallback(
            source,
            copy,
            "len",
            arguments => EnsureArgumentCount("string.len", arguments)
        );
        CopyBoundedCallback(
            source,
            copy,
            "lower",
            arguments => EnsureStringLength("string.lower", arguments, 0, false)
        );
        CopyBoundedCallback(source, copy, "rep", EnsureStringRep);
        CopyBoundedCallback(
            source,
            copy,
            "reverse",
            arguments => EnsureStringLength("string.reverse", arguments, 0, false)
        );
        CopyBoundedCallback(
            source,
            copy,
            "sub",
            arguments => EnsureStringLength("string.sub", arguments, 0, false)
        );
        CopyBoundedCallback(
            source,
            copy,
            "unicode",
            arguments => EnsureStringLength("string.unicode", arguments, 0, false)
        );
        CopyBoundedCallback(
            source,
            copy,
            "upper",
            arguments => EnsureStringLength("string.upper", arguments, 0, false)
        );
        CopyBoundedCallback(
            source,
            copy,
            "startsWith",
            arguments => EnsureStringPair("string.startsWith", arguments)
        );
        CopyBoundedCallback(
            source,
            copy,
            "endsWith",
            arguments => EnsureStringPair("string.endsWith", arguments)
        );
        CopyBoundedCallback(
            source,
            copy,
            "contains",
            arguments => EnsureStringPair("string.contains", arguments)
        );

        return copy;
    }

    private Table CreateTableLibrary()
    {
        var source = GetLibrary("table");
        var copy = new Table(_script);
        CopyBoundedCallback(source, copy, "concat", EnsureTableConcat);
        CopyBoundedCallback(
            source,
            copy,
            "insert",
            arguments =>
            {
                var table = EnsureTableLength("table.insert", arguments);

                if (table.Length >= MaximumTableElements)
                {
                    ThrowLimit("table.insert");
                }
            }
        );
        CopyBoundedCallback(
            source,
            copy,
            "pack",
            arguments => EnsureArgumentCount("table.pack", arguments)
        );
        CopyBoundedCallback(
            source,
            copy,
            "remove",
            arguments => EnsureTableLength("table.remove", arguments)
        );
        CopyBoundedCallback(source, copy, "unpack", EnsureTableUnpack);

        return copy;
    }

    private Table GetLibrary(string name)
    {
        var value = _script.Globals.Get(name);

        if (value.Type != DataType.Table)
        {
            throw new InvalidOperationException($"Lua library '{name}' is unavailable.");
        }

        return value.Table;
    }

    private static void CopyValue(Table source, Table destination, string name)
    {
        var value = source.Get(name);

        if (value.Type is not DataType.Nil and not DataType.Void)
        {
            destination.Set(name, value);
        }
    }

    private static void CopyBoundedCallback(
        Table source,
        Table destination,
        string name,
        Action<CallbackArguments> validate
    )
    {
        var value = source.Get(name);

        if (value.Type != DataType.ClrFunction)
        {
            throw new InvalidOperationException($"Lua callback '{name}' is unavailable.");
        }

        destination.Set(
            name,
            DynValue.NewCallback(
                (executionContext, arguments) =>
                {
                    validate(arguments);
                    return value.Callback.Invoke(executionContext, arguments.GetArray());
                }
            )
        );
    }

    private static void EnsureArgumentCount(
        string functionName,
        CallbackArguments arguments
    )
    {
        if (arguments.Count > MaximumCallbackArguments)
        {
            ThrowLimit(functionName);
        }
    }

    private static void EnsureStringLength(
        string functionName,
        CallbackArguments arguments,
        int index,
        bool allowNil
    )
    {
        EnsureArgumentCount(functionName, arguments);
        var value = arguments.AsType(
            index,
            functionName,
            DataType.String,
            allowNil
        );

        if (value.IsNotNil() && value.String.Length > MaximumStringCharacters)
        {
            ThrowLimit(functionName);
        }
    }

    private static void EnsureStringPair(
        string functionName,
        CallbackArguments arguments
    )
    {
        EnsureStringLength(functionName, arguments, 0, true);
        EnsureStringLength(functionName, arguments, 1, true);
    }

    private static void EnsureStringValueLength(
        string functionName,
        CallbackArguments arguments,
        int index
    )
    {
        var value = arguments[index];

        if (value.Type == DataType.String &&
            value.String.Length > MaximumStringCharacters)
        {
            ThrowLimit(functionName);
        }
    }

    private static void EnsureStringRep(CallbackArguments arguments)
    {
        const string functionName = "string.rep";
        EnsureArgumentCount(functionName, arguments);
        var value = arguments.AsType(0, functionName, DataType.String);
        var repetitions = arguments.AsType(1, functionName, DataType.Number);
        var separator = arguments.AsType(
            2,
            functionName,
            DataType.String,
            true
        );
        EnsureStringLength(functionName, arguments, 0, false);
        EnsureStringLength(functionName, arguments, 2, true);

        if (!double.IsFinite(repetitions.Number) ||
            repetitions.Number != Math.Truncate(repetitions.Number))
        {
            throw new ScriptRuntimeException(
                "bad argument #2 to 'rep' (finite integer expected)"
            );
        }

        if (repetitions.Number > MaximumStringRepetitions)
        {
            ThrowLimit(functionName);
        }

        var count = repetitions.Number < 1 ? 0L : (long)repetitions.Number;
        var separatorLength = separator.IsNil() ? 0 : separator.String.Length;
        long outputLength;

        try
        {
            outputLength = checked(
                checked((long)value.String.Length * count) +
                checked((long)separatorLength * Math.Max(count - 1, 0))
            );
        }
        catch (OverflowException)
        {
            ThrowLimit(functionName);
            return;
        }

        if (outputLength > MaximumStringCharacters)
        {
            ThrowLimit(functionName);
        }

        if (value.String.Length == 0 || count == 0)
        {
            return;
        }
    }

    private static Table EnsureTableLength(
        string functionName,
        CallbackArguments arguments
    )
    {
        EnsureArgumentCount(functionName, arguments);
        var table = arguments.AsType(
            0,
            functionName,
            DataType.Table
        ).Table;

        if (table.Length > MaximumTableElements)
        {
            ThrowLimit(functionName);
        }

        return table;
    }

    private static void EnsureTableUnpack(CallbackArguments arguments)
    {
        const string functionName = "table.unpack";
        var table = EnsureTableLength(functionName, arguments);
        var start = ReadOptionalInteger(functionName, arguments, 1, 1);
        var end = ReadOptionalInteger(functionName, arguments, 2, table.Length);
        EnsureRangeLength(functionName, start, end);
    }

    private static void EnsureTableConcat(CallbackArguments arguments)
    {
        const string functionName = "table.concat";
        var table = EnsureTableLength(functionName, arguments);
        var separatorValue = arguments.AsType(
            1,
            functionName,
            DataType.String,
            true
        );
        var separator = separatorValue.IsNil() ? "" : separatorValue.String;

        if (separator.Length > MaximumStringCharacters)
        {
            ThrowLimit(functionName);
        }

        var start = ReadOptionalInteger(functionName, arguments, 2, 1);
        var end = ReadOptionalInteger(functionName, arguments, 3, table.Length);
        var count = EnsureRangeLength(functionName, start, end);
        long outputLength = count > 0 ? separator.Length * (count - 1) : 0;

        for (long offset = 0; offset < count; offset++)
        {
            var index = (int)((long)start + offset);
            var value = table.Get(index);

            if (value.Type is not DataType.Number and not DataType.String)
            {
                return;
            }

            outputLength += value.ToPrintString().Length;

            if (outputLength > MaximumStringCharacters)
            {
                ThrowLimit(functionName);
            }
        }
    }

    private static int ReadOptionalInteger(
        string functionName,
        CallbackArguments arguments,
        int index,
        int defaultValue
    )
    {
        var value = arguments.AsType(
            index,
            functionName,
            DataType.Number,
            true
        );

        if (value.IsNil())
        {
            return defaultValue;
        }

        if (!double.IsFinite(value.Number) ||
            value.Number is < int.MinValue or > int.MaxValue)
        {
            ThrowLimit(functionName);
        }

        return (int)value.Number;
    }

    private static long EnsureRangeLength(
        string functionName,
        int start,
        int end
    )
    {
        var count = end >= start ? ((long)end - start) + 1 : 0;

        if (count > MaximumTableElements ||
            (count > 0 && end == int.MaxValue))
        {
            ThrowLimit(functionName);
        }

        return count;
    }

    private static void ThrowLimit(string functionName)
    {
        throw new ScriptRuntimeException(
            $"NPC brain native callback limit exceeded for {functionName}."
        );
    }
}
