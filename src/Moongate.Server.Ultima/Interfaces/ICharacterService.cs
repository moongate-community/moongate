using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The player characters of an account.
/// </summary>
public interface ICharacterService
{
    /// <summary>
    ///     Creates a character for the account: refuses a full account or a concurrent slot collision, puts it in the
    ///     requested slot when that is free and otherwise in the first free one, replaces every other bad choice with a
    ///     safe value, and saves the character and its starting items in one transaction. Publishes <see cref="Data.Events.CharacterCreatedEvent" /> after the commit.
    /// </summary>
    Task<CharacterCreationResult> CreateAsync(
        Serial accountId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Returns the account's player characters by slot, those pending deletion included (without a slot);
    ///     <see cref="Characters.CharacterListBuilder" /> leaves them out of the list the client sees.
    /// </summary>
    Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(Serial accountId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the character at <paramref name="listIndex" /> of the list the client was sent, with the items it
    ///     wears; null when that position is empty or out of range.
    /// </summary>
    Task<CharacterForPlay?> GetForPlayAsync(
        Serial accountId,
        int listIndex,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Returns the player characters pending deletion, of one account or of all when <paramref name="accountId" /> is
    ///     null.
    /// </summary>
    Task<IReadOnlyList<MobileEntity>> GetPendingDeletionsAsync(
        Serial? accountId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Marks for deletion the character at <paramref name="listIndex" /> of the list the client was sent. It disappears
    ///     from the list, gives up its slot and no longer counts toward the limit, and stays restorable until it is
    ///     removed. Refused when the position is empty
    ///     or out of range, or when the character is in the world. Publishes
    ///     <see cref="Data.Events.CharacterDeletionRequestedEvent" /> after the save.
    /// </summary>
    Task<CharacterDeletionResult> RequestDeletionAsync(
        Serial accountId,
        int listIndex,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Cancels the deletion of a pending character and gives it the first free slot; an account that filled up
    ///     meanwhile leaves it without one until a slot frees.
    /// </summary>
    /// <returns>
    ///     The restored character, or null when <paramref name="characterId" /> is not a player character pending deletion.
    /// </returns>
    Task<MobileEntity?> RestoreAsync(Serial characterId, CancellationToken cancellationToken = default);
}
