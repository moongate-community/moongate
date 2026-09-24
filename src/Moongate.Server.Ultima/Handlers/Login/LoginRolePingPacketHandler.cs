using Moongate.Network.Packets.General;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Ultima.Handlers.Login;

/// <summary>Echoes login keepalive pings on the originating connection.</summary>
public sealed class LoginRolePingPacketHandler : ILoginPacketHandler<PingPacket>
{
    private readonly ILoginPacketSendService _sender;

    public LoginRolePingPacketHandler(ILoginPacketSendService sender)
    {
        _sender = sender;
    }

    public async ValueTask HandleAsync(LoginSession session, PingPacket packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (session.NetworkSession.Client is not { } connection)
        {
            return;
        }

        if (!_sender.TrySend(session.SessionId, connection, packet))
        {
            await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
