using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Utils;

internal static class ItemPacketHelper
{
    public static ObjectInformationPacket CreateObjectInformationPacket(UOItemEntity item, GameSession session)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(session);

        return CreateObjectInformationPacket(item, session.AccountType);
    }

    public static ObjectInformationPacket CreateObjectInformationPacket(UOItemEntity item, AccountType accountType)
    {
        ArgumentNullException.ThrowIfNull(item);

        var flags = accountType >= AccountType.GameMaster ? ObjectInfoFlags.Movable : ObjectInfoFlags.None;

        return new(item, flags: flags);
    }
}
