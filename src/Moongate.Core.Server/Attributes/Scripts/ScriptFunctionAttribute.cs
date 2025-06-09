namespace Moongate.Core.Server.Attributes.Scripts;

[AttributeUsage(AttributeTargets.Method)]
public class ScriptFunctionAttribute(string? helpText = null) : Attribute
{
    public string? HelpText { get; } = helpText;
}
