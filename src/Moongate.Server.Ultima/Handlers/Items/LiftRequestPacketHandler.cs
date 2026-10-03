using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
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
///     Lets the player pick up (0x07) an item inside a container their character carries, an item inside a container
///     lying on the ground within reach, such as a treasure chest, or an item on the ground within 2 tiles in line of
///     sight, whoever dropped it; what cannot be picked up stays, for all but the staff: the item is recorded as held in the session and stays where it is
///     until the drop; a ground item leaves every screen and the sector grid meanwhile. Part of a stackable item splits
///     it: the held part keeps the serial, and the rest, with a serial from <see cref="IItemSerialPool" />, stays in place
///     and is shown with 0x25, or to everyone in range on the ground. Anything else is refused with 0x27, and the
///     character's own item, or the ground item, is shown back.
/// </summary>
public sealed class LiftRequestPacketHandler : IPacketHandler<LiftRequestPacket>
{
    public const string PickupFunction = "on_pickup";
    public const string CanPickUpFunction = "can_pick_up";

    private readonly ILogger _logger = Log.ForContext<LiftRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IWorldViewService _view;
    private readonly IItemSerialPool _serials;
    private readonly ITileDataService _tiles;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IItemScriptService? _scripts;
    private readonly IBankService? _bank;
    private readonly IItemTemplateService? _templates;
    private readonly ISessionService? _sessions;

    public LiftRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IWorldViewService view,
        IItemSerialPool serials,
        ITileDataService tiles,
        IPacketSendService sender,
        ITooltipService tooltips,
        IItemScriptService? scripts = null,
        IBankService? bank = null,
        IItemTemplateService? templates = null,
        ISessionService? sessions = null
    )
    {
        _sessions = sessions;
        _templates = templates;
        _bank = bank;
        _tooltips = tooltips;
        _scripts = scripts;
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
        var worn = item is not null && IsOwnWornItem(session, item);
        // The chest on the ground the item lies in, at any depth.
        var chest = item is null || onGround ? null : _items.GetGroundRoot(item);

        // An item inside a carried container or inside a chest on the ground, one the character wears (from the
        // paperdoll), or on the ground.
        if (!session.CharacterId.IsValid ||
            item is null ||
            (!onGround && !worn && chest is null && (item.ContainerId is null || _items.GetOwner(item) != session.CharacterId)) ||
            packet.Amount <= 0 ||
            packet.Amount > item.Amount ||
            // A worn stack is taken whole: the rest of a split would be a second item on the same layer.
            (packet.Amount < item.Amount && (worn || !IsStackable(item))))
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        // What lies in a bank box is reached only while the bank is open.
        if (_bank is not null && _mobiles.TryGet(session.CharacterId, out var character) && !_bank.CanAccess(session, character, item))
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        if (onGround && !CanReachFromTheGround(session, item, out var reason))
        {
            Refuse(session, reason, item);

            return;
        }

        // A chest someone holds is in nobody's reach; an item of a chest stays in it while it is held, so a second
        // hand could take it too.
        if (chest is not null &&
            !(_items.IsLyingOnGround(chest) && CanReachFromTheGround(session, chest, out _) && !IsHeldByAnother(session, item)))
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        // What cannot be picked up, such as a treasure chest or a lamp post, stays where it is for all but the staff.
        if (session.AccountType < AccountType.GameMaster && !IsMovable(item))
        {
            Refuse(session, LiftRejectReasonType.CannotLift, item);

            return;
        }

        // Last, once the rules allow the lift and before a stack is split: the item's script may still refuse it. It
        // tells the player why itself, so the client shows no message of its own.
        if (!_scripts.Allows(item, CanPickUpFunction, (long)session.CharacterId.Value))
        {
            Refuse(session, LiftRejectReasonType.Inspecific, item);

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
                _sender.TrySend(session.SessionId, _tooltips.Info(rest));

                // Those around the chest lose the pile below: they are shown what stays of it.
                if (chest is not null)
                {
                    _view.ContainedItemAppeared(rest, chest, session.CharacterId);
                }
            }
        }

        session.Set(ItemSessionKeys.Held, new(item.Id));
        _scripts?.Queue(item, PickupFunction, (long)session.CharacterId.Value);

        if (onGround)
        {
            _items.Hide(item);
            _view.ItemDisappeared(item);
        }
        else if (chest is not null)
        {
            // Those who look into the chest see it go.
            _view.ContainedItemDisappeared(item, chest, session.CharacterId);
        }
        else if (worn && _mobiles.TryGet(session.CharacterId, out var wearer))
        {
            // It stays on until it is dropped; the others see it taken off now.
            _view.WornItemRemoved(wearer, item);
        }
    }

    // The character's own worn item, except the backpack and the bank box, which never leave it.
    private static bool IsOwnWornItem(GameSession session, ItemEntity item)
    {
        return item.MobileId == session.CharacterId && item.Layer is { } layer and not (LayerType.Backpack or LayerType.Bank);
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

    private bool IsHeldByAnother(GameSession session, ItemEntity item)
    {
        return _sessions is not null &&
               _sessions.GetAll().Any(other => other.SessionId != session.SessionId && other.Get(ItemSessionKeys.Held)?.Item == item.Id);
    }

    // The item's own setting, else its template's, else the weight the client's tiledata gives to what cannot be lifted.
    private bool IsMovable(ItemEntity item)
    {
        if (item.Movable is { } movable)
        {
            return movable;
        }

        if (_templates is not null && _templates.TryGet(item.TemplateId, out var template))
        {
            return template.EffectiveMovable(_tiles);
        }

        return !(_tiles.TryGetItem(item.ItemId, out var tile) && tile.Weight == ItemTemplateExtensions.TiledataWeightCannotLift);
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
        else if (item?.MobileId is not null && item.MobileId == session.CharacterId)
        {
            // Put back on the paperdoll the client took it off.
            _sender.TrySend(session.SessionId, new WornItemPacket(item));
        }
        else if (item?.ContainerId is not null &&
                 session.CharacterId.IsValid &&
                 _items.GetOwner(item) == session.CharacterId)
        {
            _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
        }
    }
}
