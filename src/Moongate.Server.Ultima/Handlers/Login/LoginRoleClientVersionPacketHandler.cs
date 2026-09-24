using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class LoginRoleClientVersionPacketHandler : ILoginPacketHandler<ClientVersionPacket>
{
    public ValueTask HandleAsync(
        LoginSession session,
        ClientVersionPacket packet,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.NetworkSession.SetClientVersion(packet.Version);

        return ValueTask.CompletedTask;
    }
}
