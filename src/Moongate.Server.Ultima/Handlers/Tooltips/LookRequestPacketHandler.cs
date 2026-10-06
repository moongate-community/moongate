using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;

namespace Moongate.Server.Ultima.Handlers.Tooltips;

/// <summary>
///     Answers a single click (0x09) as ModernUO does with tooltips on: the first tooltip line, the name, shown over the
///     object with a localized message (0xC1), only for what the character can see.
/// </summary>
public sealed class LookRequestPacketHandler : IPacketHandler<LookRequestPacket>
{
    private readonly ITooltipService _tooltips;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public LookRequestPacketHandler(
        ITooltipService tooltips, IItemService items, IMobileService mobiles, IPacketSendService sender
    )
    {
        _tooltips = tooltips;
        _items = items;
        _mobiles = mobiles;
        _sender = sender;
    }

    public void Handle(GameSession session, LookRequestPacket packet)
    {
        if (!_tooltips.TryBuildFor(session.CharacterId, packet.Target, out var list, session.AccountType) ||
            list.Entries.Count == 0)
        {
            return;
        }

        var name = list.Entries[0];

        if (_mobiles.TryGet(packet.Target, out var mobile))
        {
            _sender.TrySend(
                session.SessionId,
                new LocalizedMessagePacket(mobile.Id, mobile.Body, name.Cliloc, mobile.Name, name.Arguments)
            );
        }
        else if (_items.TryGet(packet.Target, out var item))
        {
            _sender.TrySend(
                session.SessionId,
                new LocalizedMessagePacket(item.Id, item.ItemId, name.Cliloc, "", name.Arguments)
            );
        }
    }
}
