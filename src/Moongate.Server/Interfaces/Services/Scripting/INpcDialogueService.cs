using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Orchestrates intelligent NPC dialogue for speech and idle triggers.
/// </summary>
public interface INpcDialogueService
{
    /// <summary>
    /// Queues autonomous idle chatter generation for the NPC.
    /// </summary>
    bool QueueIdle(UOMobileEntity npc);

    /// <summary>
    /// Queues nearby player speech heard by the NPC for asynchronous AI dialogue generation.
    /// </summary>
    bool QueueListener(UOMobileEntity npc, UOMobileEntity sender, string text);
}
