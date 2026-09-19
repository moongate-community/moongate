using System.Text.RegularExpressions;

namespace Moongate.Scripting.Attributes.Scripts;

/// <summary>Marks a class as a module exposed to Lua under the given global name.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed partial class ScriptModuleAttribute : Attribute
{
    /// <summary>Gets the Lua global under which the module table is published.</summary>
    public string Name { get; }

    /// <summary>Gets the description written to the editor definitions, if any.</summary>
    public string? HelpText { get; }

    public ScriptModuleAttribute(string name, string? helpText = null)
    {
        if (!LuaIdentifier().IsMatch(name))
        {
            throw new ArgumentException(
                $"'{name}' is not a valid module name: use a lower-case Lua identifier such as 'log'.", nameof(name));
        }

        Name = name;
        HelpText = helpText;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex LuaIdentifier();
}
