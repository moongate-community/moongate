using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Handlers.Items.Internal;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     The player dropped the held item on a paperdoll (0x13): on their own character, an item that has a layer (the
///     item's own, whatever the client suggests, as ModernUO) that <see cref="IEquipmentService" /> allows is put on
///     and shown to everyone in range with 0x2E. Anything else bounces back to where it still is. The hand is always
///     freed.
/// </summary>
public sealed class EquipRequestPacketHandler : IPacketHandler<EquipRequestPacket>
{
    public const string CanEquipFunction = "can_equip";

    private readonly ILogger _logger = Log.ForContext<EquipRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IEquipmentService _equipment;
    private readonly IWorldViewService _view;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IItemScriptService? _scripts;

    private readonly IInventoryMutationGuard? _inventory;
    private readonly IMobileStateService? _state;

    public EquipRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IEquipmentService equipment,
        IWorldViewService view,
        IPacketSendService sender,
        ITooltipService tooltips,
        IItemScriptService? scripts = null,
        IInventoryMutationGuard? inventory = null,
        IMobileStateService? state = null
    )
    {
        _state = state;
        _inventory = inventory;
        _scripts = scripts;
        _tooltips = tooltips;
        _items = items;
        _mobiles = mobiles;
        _equipment = equipment;
        _view = view;
        _sender = sender;
    }

    public void Handle(GameSession session, EquipRequestPacket packet)
    {
        if (_inventory is not null && (!_inventory.AllowsOwner(session.CharacterId) ||
            (_items.TryGet(packet.Item, out var guarded) && !_inventory.Allows(guarded)) ||
            (session.Get(ItemSessionKeys.Held) is { } hand && _items.TryGet(hand.Item, out var heldItem) && !_inventory.Allows(heldItem)) || !_inventory.AllowsOwner(packet.Mobile)))
        {
            return;
        }

        var held = session.Get(ItemSessionKeys.Held);
        session.Set(ItemSessionKeys.Held, null);

        // A ghost keeps nothing in its hands: what it held went into its corpse.
        if (_mobiles.TryGet(session.CharacterId, out var ghost) && ghost.IsDead)
        {
            return;
        }

        if (held is not null && held.Item != packet.Item && _items.TryGet(held.Item, out var other))
        {
            // The hand is freed either way, so the held item must go back where it still is.
            _logger.Debug("Session {SessionId} named {Item} while holding {Held}", session.SessionId, packet.Item, other);
            HeldItemBounce.Return(session, other, _items, _mobiles, _view, _sender, _tooltips);

            return;
        }

        if (held is null || !_items.TryGet(held.Item, out var item))
        {
            _logger.Debug("Session {SessionId} tried to wear {Item} without holding it", session.SessionId, packet.Item);

            return;
        }

        // One item per layer: a stack goes on only as a single item, as a worn stack could not be split later.
        if (packet.Mobile == session.CharacterId &&
            item.Amount == 1 &&
            _mobiles.TryGet(session.CharacterId, out var character) &&
            _equipment.TryGetLayer(item, out var layer) &&
            _equipment.CanWear(character.Id, item, layer) &&
            // Last, once the rules allow it: the item's script may still refuse to be worn. Not asked of an item lifted
            // from the paperdoll and put back: it never left its layer.
            (item.MobileId == character.Id || Ask(session, held, item, character.Id)))
        {
            _items.Equip(item, character.Id, layer);
            _view.WornItemChanged(character, item);

            // What it wields and wears is in the status: its damage and its armor rating.
            _state?.SendStatus(session, character);

            return;
        }

        _logger.Debug("{Item} cannot be worn by {Mobile}: it bounces back", item, packet.Mobile);
        HeldItemBounce.Return(session, item, _items, _mobiles, _view, _sender, _tooltips);
    }

    // The item counts as held while its script is asked, so item.delete, item.consume and the like refuse it.
    private bool Ask(GameSession session, HeldItem held, ItemEntity item, Serial wearer)
    {
        session.Set(ItemSessionKeys.Held, held);

        try
        {
            return _scripts.Allows(item, CanEquipFunction, (long)wearer.Value);
        }
        finally
        {
            session.Set(ItemSessionKeys.Held, null);
        }
    }
}
