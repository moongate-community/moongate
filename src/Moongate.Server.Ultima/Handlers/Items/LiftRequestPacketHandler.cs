using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     Lets the player pick up (0x07) an item inside a container their character carries, or an item on the ground within
///     2 tiles in line of sight, whoever dropped it: the item is recorded as held in the session and stays where it is
///     until the drop; a ground item leaves every screen and the sector grid meanwhile. Part of a stackable item splits
///     it: the held part keeps the serial, and the rest, with a serial from <see cref="IItemSerialPool" />, stays in place
///     and is shown with 0x25, or to everyone in range on the ground. Anything else is refused with 0x27, and the
///     character's own item, or the ground item, is shown back.
/// </summary>
public sealed class LiftRequestPacketHandler : IPacketHandler<LiftRequestPacket>
{
    private readonly ILogger _logger = Log.ForContext<LiftRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly IItemSerialPool _serials;
    private readonly ITileDataService _tiles;
    private readonly IPacketSendService _sender;

    public LiftRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IWorldViewService view,
        IItemSerialPool serials,
        ITileDataService tiles,
        IPacketSendService sender
    )
    {
        _items = items;
        _mobiles = mobiles;
        _view = view;
        _serials = serials;
        _tiles = tiles;
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

        var onGround = item?.GroundLocation is not null;

        // An item inside a carried container, or on the ground: worn items need the paperdoll.
        if (!session.CharacterId.IsValid ||
            item is null ||
            (!onGround && (item.ContainerId is null || _items.GetOwner(item) != session.CharacterId)) ||
            packet.Amount <= 0 ||
            packet.Amount > item.Amount ||
            (packet.Amount < item.Amount && !IsStackable(item)))
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        if (onGround && !CanReachFromTheGround(session, item, out var reason))
        {
            Refuse(session, reason, item);

            return;
        }

        if (packet.Amount < item.Amount)
        {
            if (!_serials.TryTake(out var serial))
            {
                Refuse(session, LiftRejectReasonType.Inspecific, item);

                return;
            }

            var rest = _items.Split(item, packet.Amount, serial);

            if (onGround)
            {
                _view.ItemAppeared(rest);
            }
            else
            {
                _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(rest, session.UsesContainerGrid()));
            }
        }

        session.Set(ItemSessionKeys.Held, new(item.Id));

        if (onGround)
        {
            _items.Hide(item);
            _view.ItemDisappeared(item);
        }
    }

    private bool CanReachFromTheGround(GameSession session, ItemEntity item, out LiftRejectReasonType reason)
    {
        reason = LiftRejectReasonType.CannotLift;

        if (!_mobiles.TryGet(session.CharacterId, out var mobile))
        {
            return false;
        }

        if (_items.CanReach(mobile, item))
        {
            return true;
        }

        var spot = item.GroundLocation!.Value;
        var near = item.Map == mobile.Map &&
                   Math.Abs(spot.X - mobile.Location.X) <= ItemService.GroundReach &&
                   Math.Abs(spot.Y - mobile.Location.Y) <= ItemService.GroundReach;
        reason = near ? LiftRejectReasonType.OutOfSight : LiftRejectReasonType.OutOfRange;

        return false;
    }

    private bool IsStackable(ItemEntity item)
    {
        return _tiles.TryGetItem(item.ItemId, out var tile) && (tile.Flags & TileFlagType.Generic) != 0;
    }

    private void Refuse(GameSession session, LiftRejectReasonType reason, ItemEntity? item)
    {
        _logger.Debug("Session {SessionId} cannot lift {Item}: {Reason}", session.SessionId, item, reason);
        _sender.TrySend(session.SessionId, new LiftRejectPacket(reason));

        // A ground item is shown again where it lies; only the character's own items are shown back from containers:
        // another player's item must not be revealed.
        if (item?.GroundLocation is not null)
        {
            // Only to this player, and only when it lies there: an item someone holds must stay off every screen.
            if (_items.IsLyingOnGround(item) && _mobiles.TryGet(session.CharacterId, out var mobile))
            {
                _view.ShowItemTo(mobile, item);
            }
        }
        else if (item?.ContainerId is not null &&
                 session.CharacterId.IsValid &&
                 _items.GetOwner(item) == session.CharacterId)
        {
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
        }
    }
}
