using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class CharacterMoveHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<CharacterMoveHandler>();

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is MoveRequestPacket moveRequestPacket)
        {
            await ProcessMoveRequestAsync(session, moveRequestPacket);
        }
    }

    private async Task ProcessMoveRequestAsync(GameSession session, MoveRequestPacket packet)
    {
        session.Move(packet.Direction);

        if (session.MoveSequence == 255)
        {
            session.MoveSequence = 0;
        }

        session.MoveSequence++;

        _logger.Debug(
            "Processing move request for session {SessionId} with sequence {MoveSequence} Direction {Direction}",
            session.SessionId,
            session.MoveSequence,
            packet.Direction
        );
        var moveAckPacket = new MoveAckPacket(session.Mobile, session.MoveSequence);

        session.SendPackets(moveAckPacket);
    }
}
