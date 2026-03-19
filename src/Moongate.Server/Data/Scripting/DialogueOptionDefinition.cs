using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// One authored dialogue option.
/// </summary>
public sealed class DialogueOptionDefinition
{
    /// <summary>
    /// Gets or sets the player-facing option text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target node id to jump to.
    /// </summary>
    public string? GotoNodeId { get; set; }

    /// <summary>
    /// Gets or sets a built-in action id.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Gets or sets an optional Lua condition callback.
    /// </summary>
    public Closure? Condition { get; set; }

    /// <summary>
    /// Gets or sets an optional Lua effects callback.
    /// </summary>
    public Closure? Effects { get; set; }
}
