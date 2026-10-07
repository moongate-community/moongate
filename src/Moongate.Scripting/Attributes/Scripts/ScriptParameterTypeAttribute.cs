namespace Moongate.Scripting.Attributes.Scripts;

/// <summary>
///     Names the Lua type written for one parameter in the generated definitions, in place of the type derived from its
///     CLR type; for example an alias that lists the accepted strings.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ScriptParameterTypeAttribute : Attribute
{
    /// <summary>
    ///     Gets the Lua type name.
    /// </summary>
    public string LuaType { get; }

    /// <param name="luaType">
    ///     The Lua type name, as LuaLS reads it.
    /// </param>
    public ScriptParameterTypeAttribute(string luaType)
    {
        LuaType = luaType;
    }
}
