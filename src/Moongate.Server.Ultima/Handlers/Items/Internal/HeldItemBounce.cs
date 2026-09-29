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
///     container (0x25 to the holder).
/// </summary>
internal static class HeldItemBounce
{
    public static void Return(
        GameSession session,
        ItemEntity item,
        IItemService items,
        IMobileService mobiles,
        IWorldViewService view,
        IPacketSendService sender
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

        sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
    }
}
