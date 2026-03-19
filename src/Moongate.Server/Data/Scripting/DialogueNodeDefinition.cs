using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// One authored dialogue node.
/// </summary>
public sealed class DialogueNodeDefinition
{
    /// <summary>
    /// Gets or sets the node id.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node text spoken or shown to the listener.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets visible options for this node.
    /// </summary>
    public List<DialogueOptionDefinition> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional Lua on-enter callback.
    /// </summary>
    public Closure? OnEnter { get; set; }
}
