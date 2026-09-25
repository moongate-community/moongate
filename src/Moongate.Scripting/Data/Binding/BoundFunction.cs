using System.Reflection;

namespace Moongate.Scripting.Data.Binding;

/// <summary>
///     A module method as published to Lua.
/// </summary>
/// <param name="LuaName">
///     The name the function is published under in the module's Lua table.
/// </param>
/// <param name="Method">
///     The bound CLR method.
/// </param>
/// <param name="HelpText">
///     The help text from the [ScriptFunction] attribute, if any.
/// </param>
public sealed record BoundFunction(string LuaName, MethodInfo Method, string? HelpText);
