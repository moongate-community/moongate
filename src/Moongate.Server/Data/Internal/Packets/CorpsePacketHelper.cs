using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Packets;

/// <summary>
/// Emits corpse-specific packets required by the classic client visual model.
/// </summary>
public static class CorpsePacketHelper
{
    public static void EnqueueVisibleCorpsePackets(UOItemEntity item, Action<IGameNetworkPacket> enqueue)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(enqueue);

        if (!IsCorpse(item))
        {
            return;
        }

        enqueue(new AddMultipleItemsToContainerPacket(item));
        enqueue(new CorpseClothingPacket(item));
    }

    public static bool IsCorpse(UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.ItemId == CorpsePropertyKeys.ItemId &&
               item.TryGetCustomBoolean(CorpsePropertyKeys.IsCorpse, out var isCorpse) &&
               isCorpse;
    }
}
