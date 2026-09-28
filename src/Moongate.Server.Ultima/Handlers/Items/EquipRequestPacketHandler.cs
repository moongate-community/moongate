using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Items;

/// <summary>
///     The player dropped the held item on a paperdoll (0x13). Equipping is not built yet: the item bounces back to
///     its container with 0x25 and the hand is freed, so the next pick-up works.
/// </summary>
public sealed class EquipRequestPacketHandler : IPacketHandler<EquipRequestPacket>
{
    private readonly ILogger _logger = Log.ForContext<EquipRequestPacketHandler>();
    private readonly IItemService _items;
    private readonly IPacketSendService _sender;

    public EquipRequestPacketHandler(IItemService items, IPacketSendService sender)
    {
        _items = items;
        _sender = sender;
    }

    public void Handle(GameSession session, EquipRequestPacket packet)
    {
        var held = session.Get(ItemSessionKeys.Held);
        session.Set(ItemSessionKeys.Held, null);

        if (held is null || !_items.TryGet(held.Item, out var item) || item.ContainerId is null)
        {
            return;
        }

        _logger.Debug("{Item} dropped on a paperdoll bounces back: equipping is not built yet", item);
        _sender.TrySend(session.SessionId, new ContainerItemUpdatePacket(item, session.UsesContainerGrid()));
    }
}
