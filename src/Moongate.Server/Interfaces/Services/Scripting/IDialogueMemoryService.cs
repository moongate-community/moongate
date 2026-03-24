using Moongate.Server.Data.Scripting;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Loads and persists typed NPC dialogue memory files.
/// </summary>
public interface IDialogueMemoryService
{
    /// <summary>
    /// Gets or creates the typed memory entry for the specified npc and interacting mobile.
    /// </summary>
    DialogueMemoryEntry GetOrCreateEntry(Serial npcId, Serial otherMobileId);

    /// <summary>
    /// Loads the NPC memory file or creates a default one when missing.
    /// </summary>
    NpcDialogueMemoryFile LoadOrCreate(Serial npcId);

    /// <summary>
    /// Marks the NPC memory file dirty after in-memory mutation.
    /// </summary>
    void MarkDirty(Serial npcId);

    /// <summary>
    /// Persists the NPC memory file when dirty.
    /// </summary>
    void Save(Serial npcId);
}
