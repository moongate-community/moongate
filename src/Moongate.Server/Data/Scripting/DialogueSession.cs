using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Active runtime dialogue session between one NPC and one listener.
/// </summary>
public sealed class DialogueSession
{
    public Serial NpcId { get; set; }

    public Serial ListenerId { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    public string CurrentNodeId { get; set; } = string.Empty;

    public string? LastTopicId { get; set; }

    public Dictionary<string, bool> SessionFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<DialogueOptionDefinition> VisibleOptions { get; set; } = [];

    public string? PendingAction { get; set; }

    public DateTime StartedUtc { get; set; }

    public DateTime LastUpdatedUtc { get; set; }
}
