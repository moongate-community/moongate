using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.PacketHandlers;

public class ClickHandler : IGamePacketHandler
{
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
    }

    private async Task HandleSingleClickAsync(GameSession session, SingleClickPacket packet)
    {
    }
}
