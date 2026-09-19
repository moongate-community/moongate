using System.Text.RegularExpressions;

namespace Moongate.Scripting.Attributes.Scripts;

/// <summary>Marks a public instance method of a module as callable from Lua.</summary>
/// <remarks>Without a name override the method name is converted to snake_case: <c>LogInfo</c> becomes <c>log_info</c>.</remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed partial class ScriptFunctionAttribute : Attribute
{
    /// <summary>Gets the Lua name override, or null to derive it from the method name.</summary>
    public string? Name { get; }

    /// <summary>Gets the description written to the editor definitions, if any.</summary>
    public string? HelpText { get; }

    /// <summary>Initializes a new instance of the <see cref="ScriptFunctionAttribute"/> class.</summary>
    /// <param name="name">Lua name for the function, or null to convert the method name to snake_case.</param>
    /// <param name="helpText">One line describing the function, written to the editor definitions.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a lower-case Lua identifier.</exception>
    public ScriptFunctionAttribute(string? name = null, string? helpText = null)
    {
        if (name is not null && !LuaIdentifier().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid Lua identifier.", nameof(name));
        }

        Name = name;
        HelpText = helpText;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex LuaIdentifier();
}
