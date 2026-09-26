namespace Moongate.Scripting.Data.Binding;

/// <summary>
///     A module constant as published to Lua, with the value read at bind time.
/// </summary>
/// <param name="LuaName">
///     The name the constant is published under in the module's Lua table.
/// </param>
/// <param name="Type">
///     The CLR type of the constant.
/// </param>
/// <param name="Value">
///     The value read from the member at bind time.
/// </param>
/// <param name="HelpText">
///     The help text from the [ScriptConstant] attribute, if any.
/// </param>
public sealed record BoundConstant(string LuaName, Type Type, object? Value, string? HelpText);
