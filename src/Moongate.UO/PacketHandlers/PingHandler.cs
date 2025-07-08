using Moongate.Core.Network.Extensions;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.System;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class PingHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<PingHandler>();

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is PingPacket pingPacket)
        {
            session.PingSequence = pingPacket.Sequence + 1;

            if (session.PingSequence > 255)
            {
                session.PingSequence = 0;
            }

            _logger.Verbose(
                "Session {SessionId} received ping with sequence {Sequence}.",
                session.SessionId,
                pingPacket.Sequence.ToPacketString()
            );

            var responsePacket = new PingPacket
            {
                Sequence = (byte)session.PingSequence
            };

            session.SendPackets(responsePacket);
        }
    }
}
