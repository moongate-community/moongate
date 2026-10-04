namespace Moongate.Scripting.Data.Binding;

/// <summary>
///     One parameter of a script function, as Lua sees it.
/// </summary>
/// <param name="Name">
///     The parameter's name, or <c>...</c> for the trailing varargs.
/// </param>
/// <param name="LuaType">
///     The Lua type name, such as <c>integer</c>, <c>DirectionType|string</c> or a declared alias.
/// </param>
/// <param name="Optional">
///     True when a script may leave the argument out.
/// </param>
/// <param name="Default">
///     The value used when the argument is left out, as a Lua token, or null when that value is nil.
/// </param>
public sealed record ParameterDescription(string Name, string LuaType, bool Optional, string? Default);
