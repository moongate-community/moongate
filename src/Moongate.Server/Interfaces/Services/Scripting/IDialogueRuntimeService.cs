using Moongate.Server.Data.Scripting;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Coordinates active authored dialogue sessions and node transitions.
/// </summary>
public interface IDialogueRuntimeService
{
    Task<DialogueSession?> StartAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        string conversationId,
        string? topicId = null,
        CancellationToken cancellationToken = default
    );

    Task<DialogueSession?> HandleTopicAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        string conversationId,
        string text,
        CancellationToken cancellationToken = default
    );

    Task<DialogueSession?> ChooseAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        int optionIndex,
        CancellationToken cancellationToken = default
    );

    bool TryGetActiveSession(Serial npcId, Serial listenerId, out DialogueSession? session);

    bool EndSession(Serial npcId, Serial listenerId);
}
