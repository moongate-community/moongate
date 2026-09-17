using Moongate.Network.Packets.General;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Handlers.General;

public sealed class PingPacketHandler : IPacketHandler<PingPacket>
{
    private readonly IPacketSendService _sender;

    public PingPacketHandler(IPacketSendService sender)
    {
        _sender = sender;
    }

    /// <inheritdoc />
    public void Handle(GameSession session, PingPacket packet)
    {
        if (!_sender.TrySend(session.SessionId, new PingPacket(packet.Sequence)))
        {
            session.NetworkSession.Client?.Dispose();
        }
    }
}
