using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Packets;

/// <summary>
/// Provides reusable helpers for enqueuing worn item packets.
/// </summary>
public static class WornItemPacketHelper
{
    /// <summary>
    /// Enumerates visible equipped items for a character and emits worn-item packets.
    /// </summary>
    /// <param name="character">Character entity with equipped item references.</param>
    /// <param name="enqueuePacket">Callback used to enqueue packet instances.</param>
    public static void EnqueueVisibleWornItems(UOMobileEntity character, Action<WornItemPacket> enqueuePacket)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(enqueuePacket);

        foreach (var (layer, itemReference) in character.EquippedItemReferences)
        {
            if (layer == ItemLayerType.Backpack || layer == ItemLayerType.Bank)
            {
                continue;
            }

            enqueuePacket(new WornItemPacket(character, itemReference, layer));
        }
    }
}
