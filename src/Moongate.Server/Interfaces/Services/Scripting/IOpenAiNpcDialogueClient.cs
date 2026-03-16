using Moongate.Server.Data.Scripting;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Wraps OpenAI dialogue generation for NPCs.
/// </summary>
public interface IOpenAiNpcDialogueClient
{
    /// <summary>
    /// Generates structured NPC dialogue for the supplied request.
    /// </summary>
    Task<NpcDialogueResponse?> GenerateAsync(
        NpcDialogueRequest request,
        CancellationToken cancellationToken = default
    );
}
