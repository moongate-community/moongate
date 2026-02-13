using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.MegaCliloc;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;

namespace Moongate.UO.PacketHandlers;

public class ToolTipHandler : IGamePacketHandler
{
    private readonly IMegaClilocService _megaClilocService;

    public ToolTipHandler(IMegaClilocService megaClilocService)
        => _megaClilocService = megaClilocService;

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is MegaClilocRequestPacket megaClilocRequestPacket)
        {
            await HandleMegaClilocRequestAsync(session, megaClilocRequestPacket);
        }
    }

    private async Task HandleMegaClilocRequestAsync(GameSession session, MegaClilocRequestPacket request)
    {
        foreach (var serial in request.Query)
        {
            session.SendPackets(await _megaClilocService.ToPacket(serial));
        }
    }
}
