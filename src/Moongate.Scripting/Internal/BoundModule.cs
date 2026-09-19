using System.Reflection;
using Lua;

namespace Moongate.Scripting.Internal;

internal sealed record BoundFunction(string LuaName, MethodInfo Method, string? HelpText);

internal sealed record BoundConstant(string LuaName, Type Type, object? Value, string? HelpText);

internal sealed record BoundModule(
    string Name,
    string? HelpText,
    Type ModuleType,
    LuaTable Table,
    IReadOnlyList<BoundFunction> Functions,
    IReadOnlyList<BoundConstant> Constants
);
