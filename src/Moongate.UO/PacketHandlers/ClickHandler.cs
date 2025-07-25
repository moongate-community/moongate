using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Mouse;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class ClickHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<ClickHandler>();

    private readonly IItemService _itemService;

    public ClickHandler(IItemService itemService)
    {
        _itemService = itemService;
    }


    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is SingleClickPacket singleClickPacket)
        {
            await HandleSingleClickAsync(session, singleClickPacket);
            return;
        }

        if (packet is DoubleClickPacket doubleClickPacket)
        {
            await HandleDoubleClickAsync(session, doubleClickPacket);
            return;
        }

        if (packet is TargetCursorPacket targetCursorPacket)
        {
            await HandleSelectTarget(session, targetCursorPacket);
            return;
        }
    }


    private async Task HandleSelectTarget(GameSession session, TargetCursorPacket targetCursor)
    {
    }

    private async Task HandleDoubleClickAsync(GameSession session, DoubleClickPacket packet)
    {
        if (packet.IsPaperdoll)
        {
            session.SendPackets(new PaperdollPacket(session.Mobile));
            return;
        }

        if (packet.TargetSerial.IsItem)
        {
            var item = _itemService.GetItem(packet.TargetSerial);


            if (packet.TargetSerial == session.Mobile.GetBackpack().Id)
            {
                session.SendPackets(new DrawContainerAndAddItemCombinedPacket(session.Mobile.GetBackpack()));

                return;
            }

            if (item.IsContainer)
            {
                session.SendPackets(new DrawContainerAndAddItemCombinedPacket(item));

                return;
            }

            _logger.Information("Double-clicking item {ItemId} ({ItemName})", item.Id, item.Name);


            _itemService.UseItem(item, session.Mobile);
        }
    }

    private async Task HandleSingleClickAsync(GameSession session, SingleClickPacket packet)
    {
    }
}
