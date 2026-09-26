using Moongate.Network.Packets.Data.Clients;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class LoginRoleClientVersionPacketHandler : ILoginPacketHandler<ClientVersionPacket>
{
    private readonly ILogger _logger = Log.ForContext<LoginRoleClientVersionPacketHandler>();

    public ValueTask HandleAsync(
        LoginSession session,
        ClientVersionPacket packet,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ClientVersion.TryParse(packet.Version, out var clientVersion))
        {
            session.NetworkSession.SetClientVersion(clientVersion);
        }
        else
        {
            _logger.Warning("Ignoring unreadable client version {Version}", packet.Version);
        }

        return ValueTask.CompletedTask;
    }
}
