namespace Moongate.Server.Data.Scripting;

/// <summary>
/// JSON-ready contract for mapping a brain id to a Lua script file.
/// </summary>
public sealed class LuaBrainDefinition
{
    /// <summary>
    /// Logical brain id referenced by mobile templates.
    /// </summary>
    public string BrainId { get; set; }

    /// <summary>
    /// Script file path, for example <c>scripts/ai/orc_warrior.lua</c>.
    /// </summary>
    public string ScriptPath { get; set; }
}
