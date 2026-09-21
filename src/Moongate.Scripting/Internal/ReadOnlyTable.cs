using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Wraps a table so Lua can read and call through it but never assign. An empty proxy forwards reads via
/// __index; writes to existing and new keys both reach __newindex, which raises; __metatable blocks
/// setmetatable. Writing __newindex on the real table would not work: Lua only consults it for absent keys.
/// </summary>
internal static class ReadOnlyTable
{
    public static LuaTable Wrap(LuaTable hidden, string name)
    {
        var metatable = new LuaTable();
        metatable["__index"] = new(hidden);
        metatable["__newindex"] = new LuaFunction(
            "__newindex",
            (context, _) => throw new LuaRuntimeException(context.State, new LuaValue($"'{name}' is read-only"))
        );
        metatable["__metatable"] = new("locked");

        return new() { Metatable = metatable };
    }
}
