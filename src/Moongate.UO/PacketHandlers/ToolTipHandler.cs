using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.MegaCliloc;
using Moongate.UO.Data.Packets.MegaCliloc;
using Moongate.UO.Data.Packets.World;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
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
        var response = new MegaClilocResponsePacket();

        var entry = new MegaClilocEntry
        {
            Serial = session.Mobile.Id
        };

        response.Serial = session.Mobile.Id;
        response.Properties.Add(new MegaClilocProperty()
        {
            ClilocId = 0x1005BD,
            Text = session.Mobile.Name
        });

        session.SendPackets(response);
    }
}
