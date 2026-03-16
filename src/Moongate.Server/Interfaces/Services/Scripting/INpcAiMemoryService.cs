using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Loads and persists long-term NPC memory summaries.
/// </summary>
public interface INpcAiMemoryService
{
    /// <summary>
    /// Loads the existing memory file or creates a default one when missing.
    /// </summary>
    string LoadOrCreate(Serial npcId, string npcName);

    /// <summary>
    /// Persists the supplied memory summary for the NPC.
    /// </summary>
    void Save(Serial npcId, string memory);
}
