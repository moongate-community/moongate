namespace Moongate.Scripting.Data.Binding;

/// <summary>
///     A module read from its type alone, before any instance or Lua state exists: what binding publishes and what the
///     documentation lists.
/// </summary>
/// <param name="Name">
///     The module's Lua table name, from [ScriptModule].
/// </param>
/// <param name="HelpText">
///     The module's help text from [ScriptModule], if any.
/// </param>
/// <param name="ModuleType">
///     The CLR type the module was read from.
/// </param>
/// <param name="Functions">
///     Every [ScriptFunction] method of the module, in declaration order.
/// </param>
/// <param name="Constants">
///     Every [ScriptConstant] member of the module, with its value, in declaration order.
/// </param>
public sealed record ModuleDescription(
    string Name,
    string? HelpText,
    Type ModuleType,
    IReadOnlyList<BoundFunction> Functions,
    IReadOnlyList<BoundConstant> Constants
);
