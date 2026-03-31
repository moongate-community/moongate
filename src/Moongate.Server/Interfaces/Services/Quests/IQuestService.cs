using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Quests;

namespace Moongate.Server.Interfaces.Services.Quests;

/// <summary>
/// Defines the core player quest runtime operations.
/// </summary>
public interface IQuestService
{
    /// <summary>
    /// Returns quests that the specified NPC can currently offer to the player.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="npc">Quest giver mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Quest templates currently available from the NPC.</returns>
    Task<IReadOnlyList<QuestTemplateDefinition>> GetAvailableForNpcAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns active quest progress entries relevant to the specified NPC.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="npc">NPC mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active or ready-to-turn-in quest progress entries for that NPC.</returns>
    Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the player's quest journal entries.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active or ready-to-turn-in quest progress entries.</returns>
    Task<IReadOnlyList<QuestProgressEntity>> GetJournalAsync(
        UOMobileEntity player,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Attempts to accept a quest from the specified NPC.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="npc">Quest giver mobile.</param>
    /// <param name="questId">Quest identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when the quest was accepted; otherwise <see langword="false" />.</returns>
    Task<bool> AcceptAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        string questId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Attempts to complete a ready quest at the specified NPC.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="npc">Completion NPC.</param>
    /// <param name="questId">Quest identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true" /> when the quest was completed; otherwise <see langword="false" />.</returns>
    Task<bool> TryCompleteAsync(
        UOMobileEntity player,
        UOMobileEntity npc,
        string questId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Applies kill-progress updates for a player when a matching mobile dies.
    /// </summary>
    /// <param name="player">Player mobile credited for the kill.</param>
    /// <param name="killedMobile">Killed mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OnMobileKilledAsync(
        UOMobileEntity player,
        UOMobileEntity killedMobile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Re-evaluates inventory-state quest objectives for the specified player.
    /// </summary>
    /// <param name="player">Player mobile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReevaluateInventoryAsync(UOMobileEntity player, CancellationToken cancellationToken = default);
}
