namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Persistent dialogue memory file for one NPC.
/// </summary>
public sealed class NpcDialogueMemoryFile
{
    /// <summary>
    /// Gets or sets the owning NPC serial.
    /// </summary>
    public uint NpcId { get; set; }

    /// <summary>
    /// Gets or sets memory entries keyed by the interacting mobile serial.
    /// </summary>
    public Dictionary<uint, DialogueMemoryEntry> Entries { get; set; } = [];
}
