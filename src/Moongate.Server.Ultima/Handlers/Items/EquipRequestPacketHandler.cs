using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
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
    private readonly ILogger _logger = Log.ForContext<EquipRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IEquipmentService _equipment;
    private readonly IWorldViewService _view;
    private readonly IPacketSendService _sender;

    public EquipRequestPacketHandler(
        IItemService items,
        IMobileService mobiles,
        IEquipmentService equipment,
        IWorldViewService view,
        IPacketSendService sender
    )
    {
        _items = items;
        _mobiles = mobiles;
        _equipment = equipment;
        _view = view;
        _sender = sender;
    }

    public void Handle(GameSession session, EquipRequestPacket packet)
    {
        var held = session.Get(ItemSessionKeys.Held);
        session.Set(ItemSessionKeys.Held, null);

        if (held is null || held.Item != packet.Item || !_items.TryGet(held.Item, out var item))
        {
            _logger.Debug("Session {SessionId} tried to wear {Item} without holding it", session.SessionId, packet.Item);

            return;
        }

        if (packet.Mobile == session.CharacterId &&
            _mobiles.TryGet(session.CharacterId, out var character) &&
            _equipment.TryGetLayer(item, out var layer) &&
            _equipment.CanWear(character.Id, item, layer))
        {
            _items.Equip(item, character.Id, layer);
            _view.WornItemChanged(character, item);

            return;
        }

        _logger.Debug("{Item} cannot be worn by {Mobile}: it bounces back", item, packet.Mobile);
        HeldItemBounce.Return(session, item, _items, _mobiles, _view, _sender);
    }
}
