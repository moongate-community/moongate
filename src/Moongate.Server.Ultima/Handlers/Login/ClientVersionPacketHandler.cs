using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class ClientVersionPacketHandler : IPacketHandler<ClientVersionPacket>
{
    private readonly ILogger _logger = Log.ForContext<ClientVersionPacketHandler>();

    /// <inheritdoc />
    public void Handle(GameSession session, ClientVersionPacket packet)
    {
        _logger.Debug("Handling ClientVersionPacket with version {Version}", packet.Version);
        session.NetworkSession.SetClientVersion(packet.Version);
    }
}
