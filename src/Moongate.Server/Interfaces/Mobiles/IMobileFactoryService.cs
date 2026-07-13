using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Interfaces.Mobiles;

/// <summary>Builds mobile entities from protocol input.</summary>
public interface IMobileFactoryService
{
    /// <summary>
    /// Creates a player mobile from a character creation (0xF8) packet: identity, stats, appearance
    /// hues, starting skills and starting location. The mobile is built only, not persisted.
    /// </summary>
    MobileEntity CreatePlayerMobile(CharacterCreationPacket packet);
}
