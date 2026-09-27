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
    ///     Creates a character for the account: refuses it only when the account is at its limit or the slot is taken
    ///     or out of range, replaces every other bad choice with a safe value, and saves the character and its starting
    ///     items in one transaction. Publishes <see cref="Data.Events.CharacterCreatedEvent" /> after the commit.
    /// </summary>
    Task<CharacterCreationResult> CreateAsync(
        Serial accountId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Returns the account's player characters, by slot.
    /// </summary>
    Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(Serial accountId, CancellationToken cancellationToken = default);
}
