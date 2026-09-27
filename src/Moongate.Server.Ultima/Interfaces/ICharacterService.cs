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
    ///     Returns the account's player characters, by slot.
    /// </summary>
    Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(Serial accountId, CancellationToken cancellationToken = default);
}
