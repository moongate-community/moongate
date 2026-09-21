using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class ClientVersionPacketHandler : IPacketHandler<ClientVersionPacket>
{
    /// <inheritdoc />
    public void Handle(GameSession session, ClientVersionPacket packet)
        => session.NetworkSession.SetClientVersion(packet.Version);
}
