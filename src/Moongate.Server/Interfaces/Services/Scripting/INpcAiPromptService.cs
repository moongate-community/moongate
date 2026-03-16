namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Loads static NPC AI prompt files from templates/npc_ai_prompts.
/// </summary>
public interface INpcAiPromptService
{
    /// <summary>
    /// Attempts to load a prompt file.
    /// </summary>
    bool TryLoad(string promptFile, out string prompt);
}
