using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Accepts a packet the server recognises but does not act on yet, so the client is not disconnected for sending
///     it; each one is logged at Debug.
/// </summary>
public sealed class IgnoredPacketHandler<TPacket> : IPacketHandler<TPacket>
    where TPacket : class, IIncomingPacket<TPacket>
{
    private readonly ILogger _logger = Log.ForContext<IgnoredPacketHandler<TPacket>>();

    public void Handle(GameSession session, TPacket packet)
    {
        _logger.Debug(
            "Received {Packet} from session {SessionId}: not handled yet",
            typeof(TPacket).Name,
            session.SessionId
        );
    }
}
