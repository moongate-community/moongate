using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Interfaces.Accounts;

public interface ICharacterService
{
    /// <summary>
    /// Builds a player mobile from a character creation (0xF8) packet, persists it (allocating its
    /// serial) and links it to the owning account. Returns the persisted mobile.
    /// </summary>
    MobileEntity CreateCharacter(Serial accountId, CharacterCreationPacket packet);

    IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId);
}
