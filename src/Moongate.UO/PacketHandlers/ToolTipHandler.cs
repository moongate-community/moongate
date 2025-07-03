using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.MegaCliloc;
using Moongate.UO.Data.Packets.World;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.PacketHandlers;

public class ToolTipHandler : IGamePacketHandler
{
    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is MegaClilocRequestPacket megaClilocRequestPacket)
        {
            await HandleMegaClilocRequestAsync(session, megaClilocRequestPacket);
        }
    }

    private async Task HandleMegaClilocRequestAsync(GameSession session, MegaClilocRequestPacket request)
    {


    }
}
