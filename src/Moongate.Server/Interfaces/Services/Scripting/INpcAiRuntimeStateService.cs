using Moongate.UO.Data.Ids;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Tracks transient NPC AI bindings and request cooldowns.
/// </summary>
public interface INpcAiRuntimeStateService
{
    /// <summary>
    /// Binds a prompt file to an NPC serial at runtime.
    /// </summary>
    void BindPromptFile(Serial npcId, string promptFile);

    /// <summary>
    /// Returns the prompt file currently bound to the NPC.
    /// </summary>
    bool TryGetPromptFile(Serial npcId, out string? promptFile);

    /// <summary>
    /// Attempts to acquire the listener-response cooldown slot.
    /// </summary>
    bool TryAcquireListener(Serial npcId, long nowMilliseconds, int cooldownMilliseconds);

    /// <summary>
    /// Attempts to acquire the idle-chatter cooldown slot.
    /// </summary>
    bool TryAcquireIdle(Serial npcId, long nowMilliseconds, int cooldownMilliseconds);
}
