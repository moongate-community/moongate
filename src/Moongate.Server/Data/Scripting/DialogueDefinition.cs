using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Registered authored dialogue definition.
/// </summary>
public sealed class DialogueDefinition
{
    /// <summary>
    /// Gets or sets the stable conversation id.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start node id.
    /// </summary>
    public string StartNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets topic aliases keyed by topic id.
    /// </summary>
    public Dictionary<string, string[]> Topics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets routes from topic id to node id.
    /// </summary>
    public Dictionary<string, string> TopicRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets authored nodes keyed by node id.
    /// </summary>
    public Dictionary<string, DialogueNodeDefinition> Nodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the resolved Lua source path when known.
    /// </summary>
    public string? ScriptPath { get; set; }
}
