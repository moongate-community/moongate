using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Items;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Lets the player pick up (0x07) a whole item inside a container their character carries: the item is recorded as
///     held in the session and stays where it is until the drop. Anything else is refused with 0x27, and the item is
///     shown back in its container with 0x25.
/// </summary>
public sealed class LiftRequestPacketHandler : IPacketHandler<LiftRequestPacket>
{
    private readonly ILogger _logger = Log.ForContext<LiftRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IPacketSendService _sender;

    public LiftRequestPacketHandler(IItemService items, IPacketSendService sender)
    {
        _items = items;
        _sender = sender;
    }

    public void Handle(GameSession session, LiftRequestPacket packet)
    {
        _items.TryGet(packet.Item, out var item);

        if (session.Get(ItemSessionKeys.Held) is not null)
        {
            Refuse(session, LiftRejectReasonType.AreHolding, item);

            return;
        }

        // Only an item inside a carried container, whole: worn items need the paperdoll, splitting a stack comes later.
        if (!session.CharacterId.IsValid ||
            item is null ||
            item.ContainerId is null ||
            _items.GetOwner(item) != session.CharacterId ||
            packet.Amount != item.Amount)
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        session.Set(ItemSessionKeys.Held, new(item.Id));
    }

    private void Refuse(GameSession session, LiftRejectReasonType reason, ItemEntity? item)
    {
        _logger.Debug("Session {SessionId} cannot lift {Item}: {Reason}", session.SessionId, item, reason);
        _sender.TrySend(session.SessionId, new LiftRejectPacket(reason));

        // Only the character's own items are shown back: another player's item must not be revealed.
        if (item?.ContainerId is not null && session.CharacterId.IsValid && _items.GetOwner(item) == session.CharacterId)
        {
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
        }
    }
}
