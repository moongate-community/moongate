using Lua;

namespace Moongate.Scripting.Data.Binding;

/// <summary>A module after binding: its table and everything the definitions generator needs to describe it.</summary>
/// <param name="Name">The module's Lua table name, from [ScriptModule].</param>
/// <param name="HelpText">The module's help text from [ScriptModule], if any.</param>
/// <param name="ModuleType">The CLR type the module was bound from.</param>
/// <param name="Table">The read-only Lua table published under <paramref name="Name" />.</param>
/// <param name="Functions">Every function the binder published on this module.</param>
/// <param name="Constants">Every constant the binder published on this module.</param>
public sealed record BoundModule(
    string Name,
    string? HelpText,
    Type ModuleType,
    LuaTable Table,
    IReadOnlyList<BoundFunction> Functions,
    IReadOnlyList<BoundConstant> Constants
);
