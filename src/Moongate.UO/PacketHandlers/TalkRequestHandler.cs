using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class TalkRequestHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<TalkRequestHandler>();

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is TalkRequestPacket talkRequestPacket)
        {
            await HandleTalkRequestAsync(session, talkRequestPacket);
        }
    }

    private async Task HandleTalkRequestAsync(GameSession session, TalkRequestPacket packet)
    {
        _logger.Information(
            "Player {PlayerName} initiated conversation with NPC {NpcSerial}",
            session.Mobile.Name,
            packet.NpcSerial
        );

        // TODO: Implement NPC dialogue system
        await Task.CompletedTask;
    }
}
