using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Abstractions.Interfaces.Accounts;

public interface ICharacterService
{
    /// <summary>
    /// Builds a player mobile from a character creation (0xF8) packet, persists it (allocating its
    /// serial) and links it to the owning account. Returns the persisted mobile.
    /// </summary>
    MobileEntity CreateCharacter(Serial accountId, CharacterCreationPacket packet);

    /// <summary>
    /// Deletes the account's character in <paramref name="slot" /> — the index into
    /// <see cref="GetPlayerCharacters" /> — along with everything it owns, and unlinks it from the
    /// account. Returns null when the character was deleted, or the reason it was refused.
    /// </summary>
    DeleteResultType? DeleteCharacter(Serial accountId, int slot);

    IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId);
}
