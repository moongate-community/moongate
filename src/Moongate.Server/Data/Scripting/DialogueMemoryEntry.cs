namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Typed persistent dialogue memory for one npc-to-mobile relationship.
/// </summary>
public sealed class DialogueMemoryEntry
{
    /// <summary>
    /// Gets or sets boolean memory flags.
    /// </summary>
    public Dictionary<string, bool> Flags { get; set; } = [];

    /// <summary>
    /// Gets or sets numeric memory counters and values.
    /// </summary>
    public Dictionary<string, long> Numbers { get; set; } = [];

    /// <summary>
    /// Gets or sets string memory values.
    /// </summary>
    public Dictionary<string, string> Texts { get; set; } = [];

    /// <summary>
    /// Gets or sets the last node reached in authored dialogue.
    /// </summary>
    public string? LastNode { get; set; }

    /// <summary>
    /// Gets or sets the last matched topic alias.
    /// </summary>
    public string? LastTopic { get; set; }

    /// <summary>
    /// Gets or sets the last interaction timestamp.
    /// </summary>
    public DateTime LastInteractionUtc { get; set; }
}
