using Lua;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("probe", "Exercises every conversion the binder supports.")]
public sealed class ProbeModule
{
    public List<string> Calls { get; } = [];

    [ScriptFunction(helpText: "Adds two integers.")]
    public int Add(int left, int right)
    {
        return left + right;
    }

    [ScriptFunction("scale")]
    public double Multiply(double value, double factor = 2)
    {
        return value * factor;
    }

    [ScriptFunction]
    public string Greet(string name)
    {
        return "Hello, " + name;
    }

    [ScriptFunction]
    public bool Flip(bool value)
    {
        return !value;
    }

    [ScriptFunction]
    public long Big(long value)
    {
        return value * 2;
    }

    [ScriptFunction]
    public ProbeColour NextColour(ProbeColour colour)
    {
        return (ProbeColour)(((int)colour + 1) % 3);
    }

    [ScriptFunction]
    public void Record(string what, params object?[] extras)
    {
        Calls.Add(what + ":" + string.Join(",", extras.Select(extra => extra?.ToString() ?? "nil")));
    }

    [ScriptFunction]
    public LuaTable Pair(string key, double value)
    {
        var table = new LuaTable();
        table[key] = new LuaValue(value);

        return table;
    }

    [ScriptFunction]
    public int Count(LuaTable table)
    {
        return table.ArrayLength;
    }

    public int NotExposed()
    {
        return 0;
    }
}
