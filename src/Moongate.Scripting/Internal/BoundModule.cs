using System.Reflection;
using Lua;

namespace Moongate.Scripting.Internal;

/// <summary>A [ScriptFunction] method the binder published, and the Lua name it was published under.</summary>
internal sealed record BoundFunction(string LuaName, MethodInfo Method, string? HelpText);

/// <summary>A [ScriptConstant] member the binder published, and the value read from it.</summary>
internal sealed record BoundConstant(string LuaName, Type Type, object? Value, string? HelpText);

/// <summary>The result of binding a [ScriptModule] instance: its Lua table and every function and constant published on it.</summary>
internal sealed record BoundModule(
    string Name,
    string? HelpText,
    Type ModuleType,
    LuaTable Table,
    IReadOnlyList<BoundFunction> Functions,
    IReadOnlyList<BoundConstant> Constants
);
