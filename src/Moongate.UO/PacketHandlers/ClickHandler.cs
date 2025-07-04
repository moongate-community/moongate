using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Characters;
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
        }
        else if (packet is DoubleClickPacket doubleClickPacket)
        {
            await HandleDoubleClickAsync(session, doubleClickPacket);
        }
    }


    private async Task HandleDoubleClickAsync(GameSession session, DoubleClickPacket packet)
    {
        if ( packet.TargetSerial == session.Mobile.Id)
        {
            session.SendPackets(new PaperdollPacket(session.Mobile));
        }

        if (packet.TargetSerial.IsItem)
        {
            var item = _itemService.GetItem(packet.TargetSerial);

            _logger.Information("Double-clicking item {ItemId} ({ItemName})", item.Id, item.Name);


        }
    }

    private async Task HandleSingleClickAsync(GameSession session, SingleClickPacket packet)
    {
    }
}
