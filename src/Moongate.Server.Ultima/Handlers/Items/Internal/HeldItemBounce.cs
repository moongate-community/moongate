using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;

namespace Moongate.Server.Ultima.Handlers.Items.Internal;

/// <summary>
///     Puts a held item back where it still is, since a picked-up item does not move until it is dropped: back on its
///     wearer (0x2E to everyone in range, who were told it was taken off), lying on the ground again, or in its
///     container (0x25 and its tooltip revision, 0xDC, to the holder, and to those around when the container lies on
///     the ground).
/// </summary>
internal static class HeldItemBounce
{
    public static void Return(
        GameSession session,
        ItemEntity item,
        IItemService items,
        IMobileService mobiles,
        IWorldViewService view,
        IPacketSendService sender,
        ITooltipService tooltips
    )
    {
        if (item.MobileId is { } wearer)
        {
            if (mobiles.TryGet(wearer, out var mobile))
            {
                view.WornItemChanged(mobile, item);
            }

            return;
        }

        if (item.GroundLocation is not null)
        {
            items.Show(item);
            view.ItemAppeared(item);

            return;
        }

        // Taken from a chest on the ground: those around were told it left it.
        if (items.GetGroundRoot(item) is { } chest)
        {
            view.ContainedItemAppeared(item, chest, session.CharacterId);
        }

        sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
        // Its amount may have changed with a split since the client last read its tooltip.
        sender.TrySend(session.SessionId, tooltips.Info(item));
    }
}
