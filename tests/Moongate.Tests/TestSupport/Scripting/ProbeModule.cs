using Lua;
using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Tests.TestSupport.Scripting;

[ScriptModule("probe", "Exercises every conversion the binder supports.")]
public sealed class ProbeModule
{
    public List<string> Calls { get; } = [];

    [ScriptFunction(helpText: "Adds two integers.")]
    public int Add(int left, int right)
        => left + right;

    [ScriptFunction]
    public long Big(long value)
        => value * 2;

    [ScriptFunction]
    public int Count(LuaTable table)
        => table.ArrayLength;

    [ScriptFunction]
    public bool Flip(bool value)
        => !value;

    [ScriptFunction]
    public string Greet(string name)
        => "Hello, " + name;

    [ScriptFunction("scale")]
    public double Multiply(double value, double factor = 2)
        => value * factor;

    [ScriptFunction]
    public ProbeColour NextColour(ProbeColour colour)
        => (ProbeColour)(((int)colour + 1) % 3);

    public int NotExposed()
        => 0;

    [ScriptFunction]
    public LuaTable Pair(string key, double value)
    {
        var table = new LuaTable();
        table[key] = new(value);

        return table;
    }

    [ScriptFunction]
    public void Record(string what, params object?[] extras)
        => Calls.Add(what + ":" + string.Join(",", extras.Select(extra => extra?.ToString() ?? "nil")));
}
