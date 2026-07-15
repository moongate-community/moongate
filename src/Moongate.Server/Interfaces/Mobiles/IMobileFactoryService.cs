using Moongate.Core.Geometry;
using Moongate.Network.Packets.Incoming;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Interfaces.Mobiles;

/// <summary>Builds mobile entities. The mobiles are built only, never persisted.</summary>
public interface IMobileFactoryService
{
    /// <summary>
    /// Creates a player mobile from a character creation (0xF8) packet: identity, stats, appearance
    /// hues, starting skills and starting location. The mobile is built only, not persisted.
    /// </summary>
    MobileEntity CreatePlayerMobile(CharacterCreationPacket packet);

    /// <summary>
    /// Builds a bare mobile with the given name at a map location. The mobile is built only, not
    /// persisted; the caller obtains its serial by persisting it.
    /// </summary>
    MobileEntity Create(string name, int mapId, Point3D position);
}
