using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

/// <summary>
///     Accepts a packet the login server recognises but does not act on, so the client is not disconnected for
///     sending it; each one is logged at Debug.
/// </summary>
public sealed class LoginRoleIgnoredPacketHandler<TPacket> : ILoginPacketHandler<TPacket>
    where TPacket : class, IIncomingPacket<TPacket>
{
    private readonly ILogger _logger = Log.ForContext<LoginRoleIgnoredPacketHandler<TPacket>>();

    public ValueTask HandleAsync(LoginSession session, TPacket packet, CancellationToken cancellationToken)
    {
        _logger.Debug(
            "Received {Packet} from login session {SessionId}: not handled",
            typeof(TPacket).Name,
            session.SessionId
        );

        return ValueTask.CompletedTask;
    }
}
