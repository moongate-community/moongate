namespace Moongate.Scripting.Attributes.Scripts;

/// <summary>Marks a public static readonly field or static get-only property of a module as a Lua constant.</summary>
/// <remarks>Without a name override the member name is used as is, so <c>LEVEL_INFO</c> stays <c>LEVEL_INFO</c>.</remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ScriptConstantAttribute : Attribute
{
    /// <summary>Gets the Lua name override, or null to use the member name.</summary>
    public string? Name { get; }

    /// <summary>Gets the description written to the editor definitions, if any.</summary>
    public string? HelpText { get; }

    /// <summary>Initializes a new instance of the <see cref="ScriptConstantAttribute" /> class.</summary>
    /// <param name="name">Lua name for the constant, or null to use the member name unchanged.</param>
    /// <param name="helpText">One line describing the constant, written to the editor definitions.</param>
    public ScriptConstantAttribute(string? name = null, string? helpText = null)
    {
        Name = name;
        HelpText = helpText;
    }
}
