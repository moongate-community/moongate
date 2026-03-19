using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Structured input sent to the OpenAI NPC dialogue client.
/// </summary>
public sealed record NpcDialogueRequest
{
    public required Serial NpcId { get; init; }

    public required string NpcName { get; init; }

    public required string Prompt { get; init; }

    public required string Memory { get; init; }

    public bool IsIdle { get; init; }

    public string? SenderName { get; init; }

    public string? HeardText { get; init; }

    public IReadOnlyList<string> NearbyPlayerNames { get; init; } = [];
}
